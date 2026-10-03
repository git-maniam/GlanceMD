(() => {
  "use strict";

  const post = message => window.chrome.webview.postMessage(message);
  const decode = value => new TextDecoder().decode(Uint8Array.from(atob(value), c => c.charCodeAt(0)));

  function sanitizeSvg(svgText) {
    const document = new DOMParser().parseFromString(svgText, "image/svg+xml");
    if (document.querySelector("parsererror")) throw new Error("Mermaid produced invalid SVG.");
    document.querySelectorAll("script,foreignObject,iframe,object,embed,audio,video").forEach(node => node.remove());
    document.querySelectorAll("*").forEach(node => {
      [...node.attributes].forEach(attribute => {
        const name = attribute.name.toLowerCase();
        const value = attribute.value.trim().toLowerCase();
        if (name.startsWith("on") || (name === "href" || name.endsWith(":href")) && !value.startsWith("#")) {
          node.removeAttribute(attribute.name);
        }
      });
    });
    return new XMLSerializer().serializeToString(document.documentElement);
  }

  async function renderDiagrams() {
    const containers = [...document.querySelectorAll("[data-mermaid-source]")];
    if (!window.mermaid) {
      containers.forEach(container => showDiagramError(container, "The Mermaid runtime is unavailable."));
      post({ type: "ready", failed: containers.length });
      return;
    }

    window.mermaid.initialize({
      startOnLoad: false,
      securityLevel: "strict",
      htmlLabels: false,
      theme: document.documentElement.dataset.theme === "dark" ? "dark" : "default",
      maxTextSize: 262144,
      suppressErrorRendering: true
    });

    let failures = 0;
    for (let index = 0; index < containers.length; index++) {
      const container = containers[index];
      try {
        const source = decode(container.dataset.mermaidSource);
        const result = await Promise.race([
          window.mermaid.render(`glancemd-diagram-${index}`, source),
          new Promise((_, reject) => setTimeout(() => reject(new Error("Diagram rendering timed out.")), 10000))
        ]);
        const cleanSvg = sanitizeSvg(result.svg);
        container.querySelector(".mermaid-output").innerHTML = cleanSvg;
        container.classList.add("rendered");
      } catch (error) {
        failures++;
        showDiagramError(container, error instanceof Error ? error.message : "Invalid Mermaid diagram.");
      }
    }
    post({ type: "ready", failed: failures });
  }

  function showDiagramError(container, message) {
    const output = container.querySelector(".mermaid-output");
    output.classList.add("render-error");
    output.textContent = `Mermaid diagram could not be rendered. ${message}`;
  }

  function notify(text) {
    const status = document.getElementById("copy-status");
    status.textContent = text;
    status.classList.add("visible");
    setTimeout(() => status.classList.remove("visible"), 1200);
  }

  async function copyDiagram(container) {
    const svg = container.querySelector("svg");
    if (!svg) return;
    const serialized = new XMLSerializer().serializeToString(svg);
    const blob = new Blob([serialized], { type: "image/svg+xml;charset=utf-8" });
    const dataUrl = await new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result);
      reader.onerror = () => reject(new Error("Could not read diagram."));
      reader.readAsDataURL(blob);
    });
    const image = new Image();
    await new Promise((resolve, reject) => {
      image.onload = resolve;
      image.onerror = () => reject(new Error("Could not rasterize diagram."));
      image.src = dataUrl;
    });
    const viewBox = svg.viewBox?.baseVal;
    const width = Math.max(1, Math.ceil(viewBox?.width || svg.getBoundingClientRect().width));
    const height = Math.max(1, Math.ceil(viewBox?.height || svg.getBoundingClientRect().height));
    const scale = Math.min(2, 4096 / Math.max(width, height));
    const canvas = document.createElement("canvas");
    canvas.width = Math.ceil(width * scale);
    canvas.height = Math.ceil(height * scale);
    const context = canvas.getContext("2d");
    context.fillStyle = document.documentElement.dataset.theme === "dark" ? "#0d1117" : "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    const png = await new Promise((resolve, reject) => canvas.toBlob(value => value ? resolve(value) : reject(new Error("Could not encode diagram.")), "image/png"));
    const bytes = new Uint8Array(await png.arrayBuffer());
    let binary = "";
    bytes.forEach(byte => binary += String.fromCharCode(byte));
    post({ type: "copy-png", data: btoa(binary) });
    notify("Diagram copied");
  }

  document.addEventListener("click", async event => {
    const button = event.target.closest("button[data-copy]");
    if (button) {
      const container = button.closest(".code-container,.mermaid-container");
      if (button.dataset.copy === "code") post({ type: "copy-text", text: container.querySelector("code").textContent });
      if (button.dataset.copy === "mermaid") post({ type: "copy-text", text: decode(container.dataset.mermaidSource) });
      if (button.dataset.copy === "diagram") await copyDiagram(container);
      else notify("Copied");
      return;
    }

    const link = event.target.closest("a[href]");
    if (link && !link.getAttribute("href").startsWith("#")) {
      event.preventDefault();
      post({ type: "link", href: link.getAttribute("href") });
    }
  });

  window.glanceMD = {
    find: (text, backwards) => text ? window.find(text, false, !!backwards, true, false, true, false) : false,
    scrollTo: id => document.getElementById(id)?.scrollIntoView({ block: "start" }),
    setTheme: theme => { document.documentElement.dataset.theme = theme; },
    setZoom: factor => { document.documentElement.style.fontSize = `${Math.max(.5, Math.min(3, factor)) * 100}%`; }
  };

  renderDiagrams();
})();
