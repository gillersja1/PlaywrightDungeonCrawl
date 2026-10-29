// Learner progress, kept in this browser's localStorage.
// Shape: { scrolls: [floorSlug], riddles: [floorSlug], treasures: [roomSlug],
//          help: { "hint:locators:0": 5, "nudge:hall-of-doors:1": 10, ... } }
// Score = XP earned minus the XP spent on help.
(function () {
    const KEY = "pdc-progress";
    const rules = JSON.parse(document.getElementById("dungeon-rules").textContent);

    function empty() {
        return { scrolls: [], riddles: [], treasures: [], help: {} };
    }

    function load() {
        try {
            const saved = JSON.parse(localStorage.getItem(KEY));
            return saved ? { ...empty(), ...saved } : empty();
        } catch {
            return empty();
        }
    }

    function save(progress) {
        try {
            localStorage.setItem(KEY, JSON.stringify(progress));
        } catch {
            // Storage blocked (private mode, etc.): progress lasts for this page only.
        }
        render();
    }

    function mark(kind, slug) {
        const progress = load();
        if (!progress[kind].includes(slug)) {
            progress[kind].push(slug);
            save(progress);
            document.dispatchEvent(new CustomEvent("dungeon:earned", { detail: { kind, slug, progress } }));
            return true;
        }
        return false;
    }

    function earned(progress = load()) {
        return progress.scrolls.length * rules.scroll
            + progress.riddles.length * rules.riddle
            + progress.treasures.length * rules.treasure;
    }

    function helpSpent(progress = load()) {
        return Object.values(progress.help).reduce((sum, cost) => sum + cost, 0);
    }

    // The score: what you earned, less what you spent on hints and nudges.
    function xp(progress = load()) {
        return Math.max(0, earned(progress) - helpSpent(progress));
    }

    function hasUsedHelp(key, progress = load()) {
        return key in progress.help;
    }

    // Charges for a piece of help the first time it's opened; free after that.
    function useHelp(key, cost) {
        const progress = load();
        if (!(key in progress.help)) {
            progress.help[key] = cost;
            save(progress);
            document.dispatchEvent(new CustomEvent("dungeon:help", { detail: { key, cost, progress } }));
        }
    }

    function rank(points = xp()) {
        return rules.ranks.filter(r => points >= r.minXp).at(-1).title;
    }

    // A floor is complete once its scroll, riddle and treasure are all done.
    function isComplete(number, progress = load()) {
        const floor = rules.floors.find(f => f.number === number);
        return !!floor
            && progress.scrolls.includes(floor.slug)
            && progress.riddles.includes(floor.slug)
            && progress.treasures.includes(floor.room);
    }

    // Where to go after a challenge on this floor: its next unfinished step,
    // or, once the floor is complete, the stairs down.
    function nextSteps(number, progress = load()) {
        const floor = rules.floors.find(f => f.number === number);
        const below = rules.floors.find(f => f.number === number + 1);
        const todo = [
            !progress.scrolls.includes(floor.slug) && { href: `/floors/${floor.slug}`, label: "Read the scroll" },
            !progress.riddles.includes(floor.slug) && { href: `/floors/${floor.slug}/riddle`, label: "Face the riddle" },
            !progress.treasures.includes(floor.room) && { href: `/rooms/${floor.room}`, label: `Enter ${floor.roomName}` },
        ].filter(Boolean);
        if (todo.length > 0) {
            return { title: "Next challenge", links: [todo[0]] };
        }
        const links = [];
        if (below) {
            links.push({ href: `/floors/${below.slug}`, label: `Down to floor ${below.number}: ${below.title}` });
        }
        links.push({ href: "/", label: "Back to the Respawn Room" });
        return { title: `Floor ${number} complete!`, links };
    }

    // Floors open in order. A browser driven by a test (navigator.webdriver)
    // starts with empty progress, so it isn't gated: the lock is for people.
    function isOpen(number, progress = load()) {
        return number <= 1 || navigator.webdriver || isComplete(number - 1, progress);
    }

    function reset() {
        save(empty());
    }

    // Anything with data-progress="kind:slug" gets a "done" state.
    function render() {
        const progress = load();
        const points = xp(progress);
        const badge = document.getElementById("xp-badge");
        if (badge) {
            badge.textContent = `${points} XP · ${rank(points)}`;
        }
        document.querySelectorAll("[data-progress]").forEach(el => {
            const [kind, slug] = el.dataset.progress.split(":");
            const done = progress[kind].includes(slug);
            el.classList.toggle("done", done);
            const state = el.querySelector(".state");
            if (state) {
                state.textContent = done ? "Done" : "Not yet";
            }
        });
        document.querySelectorAll("[data-floor-gate]").forEach(card => {
            card.classList.toggle("locked", !isOpen(Number(card.dataset.floorGate), progress));
        });
        document.querySelectorAll("[data-page-gate]").forEach(page => {
            const open = isOpen(Number(page.dataset.pageGate), progress);
            page.hidden = !open;
            document.querySelectorAll("[data-sealed-note]").forEach(note => (note.hidden = open));
        });
        // A next-step panel appears once the challenge it follows is done.
        document.querySelectorAll("[data-next-step]").forEach(panel => {
            const floor = rules.floors.find(f => f.number === Number(panel.dataset.nextStep));
            const kind = panel.dataset.nextAfter;
            panel.hidden = !floor || !progress[kind].includes(kind === "treasures" ? floor.room : floor.slug);
            if (!panel.hidden) {
                const { title, links } = nextSteps(floor.number, progress);
                panel.innerHTML = `<p>${title}</p>` + links
                    .map((link, i) => `<a class="button${i > 0 ? " secondary" : ""}" href="${link.href}">${link.label} →</a>`)
                    .join("");
            }
        });
        document.dispatchEvent(new CustomEvent("dungeon:progress", { detail: { progress, xp: points, rank: rank(points) } }));
    }

    window.Dungeon = { rules, load, mark, earned, helpSpent, xp, rank, reset, render, isComplete, isOpen, nextSteps, hasUsedHelp, useHelp };
    document.addEventListener("DOMContentLoaded", render);
})();
