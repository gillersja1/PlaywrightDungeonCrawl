// Panels that roll up (<details data-rollup="name">) remember whether they were left open,
// and open by themselves when a link points inside them, like the header's "Claim treasure".
(function () {
    const KEY = "pdc-rollup:";
    const panels = [...document.querySelectorAll("details[data-rollup]")];

    function remembered(panel) {
        try {
            return localStorage.getItem(KEY + panel.dataset.rollup) === "open";
        } catch {
            return false;
        }
    }

    function openFor(hash) {
        const target = hash && document.getElementById(decodeURIComponent(hash.slice(1)));
        const panel = target && target.closest("details[data-rollup]");
        if (panel && !panel.open) {
            panel.open = true;
            target.scrollIntoView();
        }
    }

    panels.forEach(panel => {
        panel.open = remembered(panel);
        panel.addEventListener("toggle", () => {
            try {
                localStorage.setItem(KEY + panel.dataset.rollup, panel.open ? "open" : "closed");
            } catch {
                // Storage blocked: the panel just won't remember.
            }
        });
    });

    openFor(location.hash);
    window.addEventListener("hashchange", () => openFor(location.hash));
    // Clicking a link to the hash you're already on doesn't fire hashchange.
    document.addEventListener("click", e => {
        const link = e.target.closest("a[href*='#']");
        if (link && link.pathname === location.pathname) {
            openFor(link.hash);
        }
    });
})();
