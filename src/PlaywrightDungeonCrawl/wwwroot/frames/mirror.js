// Floor 5: the mirror is its own document, and its contents sit in an open shadow root.
customElements.define("magic-mirror", class extends HTMLElement {
    connectedCallback() {
        const root = this.attachShadow({ mode: "open" });
        root.innerHTML = `
            <style>
                .glass { margin: 12px; padding: 18px; border-radius: 120px 120px 12px 12px; text-align: center;
                         background: linear-gradient(160deg, #3a4a5a, #1f2830); border: 3px solid #8a7a5a; }
                .glass.broken { background: repeating-linear-gradient(120deg, #2a2730 0 14px, #3a3642 14px 16px); }
                .reflection { font-size: 2.4rem; margin: 6px 0; }
                button { background: #f2a33a; color: #2b2118; border: 0; border-radius: 8px; padding: 8px 16px;
                         font: inherit; font-weight: 600; cursor: pointer; }
                button:disabled { opacity: 0.5; cursor: default; }
                output { display: block; margin-top: 12px; font: 600 1.6rem ui-monospace, Consolas, monospace;
                         color: #f2a33a; letter-spacing: 0.15em; }
            </style>
            <div class="glass">
                <p class="reflection" aria-hidden="true">🪞</p>
                <p class="status" role="status">Your reflection stares back. It looks nervous too.</p>
                <button type="button">Shatter the mirror</button>
                <div class="behind"></div>
            </div>`;

        const glass = root.querySelector(".glass");
        const status = root.querySelector(".status");
        const button = root.querySelector("button");

        button.addEventListener("click", () => {
            // Playwright dismisses dialogs unless a test says otherwise, so confirm() returns false.
            if (!confirm("Shatter the mirror? That's seven years' bad luck.")) {
                status.textContent = "Your hand hesitates. The mirror stays whole.";
                return;
            }
            status.textContent = "Crack!";
            parent.postMessage({ type: "mirror-shattered" }, location.origin);
        });

        window.addEventListener("message", e => {
            if (e.origin !== location.origin || e.data?.type !== "mirror-rune") {
                return;
            }
            glass.classList.add("broken");
            button.disabled = true;
            status.textContent = "The glass falls away. Something is written behind it.";
            const rune = document.createElement("output");
            rune.setAttribute("aria-label", "Mirror rune");
            rune.textContent = e.data.rune;
            root.querySelector(".behind").append(rune);
        });
    }
});
