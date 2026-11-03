// Cursed Runs: twists a room enforces on itself, applied to whatever the room draws, including what it
// adds later (the next trial, a menu, a reading). Rooms never look their own elements up by test id, so
// taking the ids away can't break the room, only the tests that leaned on them.

const DECOY_SUFFIX = " (cursed)";

// The controls a test clicks: these get shuffled, re-cased and shadowed by decoys.
const CONTROLS = "button, a[href], [role=menuitem]";
// Text a test might match on: these get re-cased.
const LABELS = "label, legend";

function shuffle(list) {
    const items = [...list];
    for (let i = items.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [items[i], items[j]] = [items[j], items[i]];
    }
    return items;
}

const CASES = [s => s.toUpperCase(), s => s.toLowerCase(), s => s.replace(/\b\w/g, c => c.toUpperCase())];

/** Re-cases an element's own text, leaving any child elements (a strong, an input) as they are. */
function recase(el) {
    const change = CASES[Math.floor(Math.random() * CASES.length)];
    for (const node of el.childNodes) {
        if (node.nodeType === Node.TEXT_NODE && node.textContent.trim()) {
            node.textContent = change(node.textContent);
        }
    }
}

/** A look-alike next to the real control. Touching it springs the trap. */
function decoyFor(control, trap) {
    const decoy = document.createElement(control.tagName === "A" ? "a" : "button");
    if (decoy.tagName === "A") {
        decoy.href = "#";
    } else {
        decoy.type = "button";
    }
    decoy.className = control.className;
    decoy.classList.add("decoy");
    const role = control.getAttribute("role");
    if (role) {
        decoy.setAttribute("role", role);
    }
    decoy.textContent = control.textContent.trim() + DECOY_SUFFIX;
    decoy.dataset.cursed = "decoy";
    decoy.addEventListener("click", e => {
        e.preventDefault();
        e.stopPropagation();
        trap();
    });
    return decoy;
}

function shiftWalls(root, trap) {
    for (const control of root.querySelectorAll(CONTROLS)) {
        if (control.dataset.cursed) {
            continue;
        }
        control.dataset.cursed = "shifted";
        recase(control);
        // Always after the real one, so a room finding its own control (querySelector(".fire")) gets the
        // real one; rows of controls are shuffled below, so position still gives nothing away there.
        control.insertAdjacentElement("afterend", decoyFor(control, trap));
    }
    for (const label of root.querySelectorAll(LABELS)) {
        if (!label.dataset.cursed) {
            label.dataset.cursed = "shifted";
            recase(label);
        }
    }
    // A row made only of controls (doors, bolts, menu items) comes back in a new order. Mixed containers,
    // like a form's labels and fields, keep theirs: the curse moves things, it doesn't scramble a form.
    for (const parent of new Set([...root.querySelectorAll(CONTROLS)].map(c => c.parentElement))) {
        if (!parent || parent.dataset.shuffled || parent.children.length < 2
            || ![...parent.children].every(child => child.matches(CONTROLS))) {
            continue;
        }
        parent.dataset.shuffled = "true";
        for (const child of shuffle(parent.children)) {
            parent.append(child);
        }
    }
}

function stripTestIds(root) {
    if (root.removeAttribute) {
        root.removeAttribute("data-testid");
    }
    for (const el of root.querySelectorAll("[data-testid]")) {
        el.removeAttribute("data-testid");
    }
}

/**
 * Applies a curse to the room. `arena` is where the room draws; `trap` springs the trap.
 * Haste and Flawless are enforced by the server (the time limit, the one try), so nothing to do here.
 */
export function applyCurse(code, arena, trap) {
    let effect = null;
    if (code === "missing-ids") {
        effect = stripTestIds;
    } else if (code === "shifting-walls") {
        effect = root => shiftWalls(root, trap);
    }
    if (!effect) {
        return;
    }
    effect(arena);
    // Rooms keep drawing (trials, menus, readings): curse those too, as they appear.
    new MutationObserver(mutations => {
        for (const m of mutations) {
            if (m.type === "attributes") {
                effect(m.target);
            }
            for (const node of m.addedNodes) {
                if (node.nodeType === Node.ELEMENT_NODE && node.dataset.cursed !== "decoy") {
                    effect(node.parentElement ?? node);
                }
            }
        }
    }).observe(arena, code === "missing-ids"
        ? { childList: true, subtree: true, attributes: true, attributeFilter: ["data-testid"] }
        : { childList: true, subtree: true });
}
