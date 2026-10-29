// Reveals a trap room's nudges one at a time. Each costs XP the first time
// it's revealed; nudges already paid for stay visible.
(function () {
    const panel = document.getElementById("nudges");
    if (!panel) {
        return;
    }
    const items = [...panel.querySelectorAll(".nudge-list li")];
    const button = document.getElementById("nudge-button");

    function shown() {
        return items.filter(item => Dungeon.hasUsedHelp(item.dataset.helpKey)).length;
    }

    function render() {
        const count = shown();
        items.forEach((item, i) => (item.hidden = i >= count));
        if (count === items.length) {
            button.disabled = true;
            button.textContent = "That's every nudge. You've got this.";
            return;
        }
        const cost = Number(items[count].dataset.helpCost);
        button.textContent = cost === 0
            ? `Give me a nudge (free on this floor, ${items.length - count} left)`
            : `Give me a nudge (−${cost} XP, ${items.length - count} left)`;
    }

    button.addEventListener("click", () => {
        const next = items[shown()];
        if (next) {
            Dungeon.useHelp(next.dataset.helpKey, Number(next.dataset.helpCost));
            render();
        }
    });

    document.addEventListener("dungeon:progress", render);
    render();
})();
