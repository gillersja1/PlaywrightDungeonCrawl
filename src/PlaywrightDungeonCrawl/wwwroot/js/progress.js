// Learner progress, kept in this browser's localStorage.
// Shape: { scrolls: [floorSlug], riddles: [floorSlug], treasures: [roomSlug],
//          help: { "hint:locators:0": 5, "nudge:hall-of-doors:1": 10, ... },
//          loot: { "torch-ghostfire": 50, ... }, equipped: { title: "title-lockpicker", torch: "" } }
// Score = XP earned minus the XP spent on help. The wallet is the score minus
// what the loot shop has had; shopping never lowers the score.
// An empty string in equipped means "took it off", so that choice survives a sync.
// Signed in with GitHub, every save is also sent to the server, which merges
// it with the account's progress and sends back the combined copy.
(function () {
    const KEY = "pdc-progress";
    const rules = JSON.parse(document.getElementById("dungeon-rules").textContent);

    function empty() {
        return { scrolls: [], riddles: [], treasures: [], help: {}, loot: {}, equipped: {} };
    }

    function load() {
        try {
            const saved = JSON.parse(localStorage.getItem(KEY));
            return saved ? { ...empty(), ...saved } : empty();
        } catch {
            return empty();
        }
    }

    function store(progress) {
        try {
            localStorage.setItem(KEY, JSON.stringify(progress));
        } catch {
            // Storage blocked (private mode, etc.): progress lasts for this page only.
        }
    }

    function save(progress) {
        store(progress);
        render();
        sync();
    }

    // Sends this browser's progress to the account and keeps the merged reply.
    // Offline or signed out elsewhere: the local copy stays, and the next save retries.
    async function sync() {
        if (!rules.account.player) {
            return;
        }
        try {
            const reply = await fetch("/api/progress", {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(load()),
            });
            if (reply.ok) {
                store({ ...empty(), ...(await reply.json()) });
                render();
            }
        } catch {
            // Network trouble: try again on the next save.
        }
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

    function lootSpent(progress = load()) {
        return Object.values(progress.loot).reduce((sum, price) => sum + price, 0);
    }

    // XP left to spend in the shop. It can dip below zero if help is taken after shopping.
    function wallet(progress = load()) {
        return xp(progress) - lootSpent(progress);
    }

    function owns(id, progress = load()) {
        return id in progress.loot;
    }

    function worn(slot, progress = load()) {
        const id = progress.equipped[slot];
        return id && owns(id, progress) ? rules.shop.find(i => i.id === id) : undefined;
    }

    // Buys an item at its list price and puts it on. Returns false if the wallet can't cover it.
    function buy(id) {
        const item = rules.shop.find(i => i.id === id);
        const progress = load();
        if (!item || owns(id, progress)) {
            return !!item;
        }
        if (wallet(progress) < item.price) {
            return false;
        }
        progress.loot[id] = item.price;
        progress.equipped[item.slot] = id;
        save(progress);
        document.dispatchEvent(new CustomEvent("dungeon:loot", { detail: { item, progress } }));
        return true;
    }

    // Wears an owned item in its slot, or takes the slot off with id = "".
    function equip(slot, id) {
        const progress = load();
        if (id && !owns(id, progress)) {
            return;
        }
        progress.equipped[slot] = id;
        save(progress);
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

    async function reset() {
        if (rules.account.player) {
            await fetch("/api/progress", { method: "DELETE" }).catch(() => {});
        }
        store(empty());
        render();
    }

    // Anything with data-progress="kind:slug" gets a "done" state.
    function render() {
        const progress = load();
        const points = xp(progress);
        const badge = document.getElementById("xp-badge");
        const title = worn("title", progress);
        if (badge) {
            badge.textContent = `${points} XP · ${title ? title.name : rank(points)}`;
        }
        const torch = worn("torch", progress);
        const root = document.documentElement.style;
        if (torch) {
            document.documentElement.dataset.torch = torch.id;
            root.setProperty("--torch", torch.color);
            root.setProperty("--torch-dim", torch.dim);
            root.setProperty("--torch-bright", `color-mix(in srgb, ${torch.color} 70%, white)`);
        } else {
            delete document.documentElement.dataset.torch;
            root.removeProperty("--torch");
            root.removeProperty("--torch-dim");
            root.removeProperty("--torch-bright");
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

    window.Dungeon = { rules, load, mark, earned, helpSpent, xp, rank, reset, render, isComplete, isOpen, nextSteps, hasUsedHelp, useHelp,
        lootSpent, wallet, owns, worn, buy, equip };
    document.addEventListener("DOMContentLoaded", () => {
        render();
        sync();
    });
})();
