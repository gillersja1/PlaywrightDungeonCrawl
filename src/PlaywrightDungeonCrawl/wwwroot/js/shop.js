// The Quartermaster's loot shop on the Respawn Room. Prices and purchases live in progress.js;
// this keeps the buttons and the wallet line in step with them.
(function () {
    const status = document.getElementById("shop-status");

    function say(text) {
        if (status) {
            status.textContent = text;
        }
    }

    function render() {
        const { rules, xp, wallet, owns, worn } = window.Dungeon;
        const left = wallet();
        document.getElementById("wallet").textContent = `${left} XP`;
        document.getElementById("wallet-score").textContent = xp();

        document.querySelectorAll("[data-loot]").forEach(row => {
            const item = rules.shop.find(i => i.id === row.dataset.loot);
            const button = row.querySelector(".loot-button");
            const owned = owns(item.id);
            const wearing = worn(item.slot)?.id === item.id;
            row.classList.toggle("owned", owned);
            row.classList.toggle("worn", wearing);
            button.disabled = !owned && left < item.price;
            button.classList.toggle("secondary", owned);
            button.title = !owned && left < item.price ? `You need ${item.price - left} more XP` : "";
            if (wearing) {
                button.textContent = "Take off";
                button.setAttribute("aria-label", `Take off ${item.name}`);
            } else if (owned) {
                button.textContent = "Wear";
                button.setAttribute("aria-label", `Wear ${item.name}`);
            } else {
                button.textContent = `${item.price} XP`;
                button.setAttribute("aria-label", `Buy ${item.name} for ${item.price} XP`);
            }
        });
    }

    document.addEventListener("click", event => {
        const button = event.target.closest(".loot-button");
        if (!button) {
            return;
        }
        const { rules, owns, worn, buy, equip } = window.Dungeon;
        const item = rules.shop.find(i => i.id === button.closest("[data-loot]").dataset.loot);
        if (worn(item.slot)?.id === item.id) {
            equip(item.slot, "");
            say(`You take off the ${item.name}. The quartermaster pretends not to notice.`);
        } else if (owns(item.id)) {
            equip(item.slot, item.id);
            say(`${item.name}: on.`);
        } else if (buy(item.id)) {
            say(`Sold! ${item.name} for ${item.price} XP. Your score hasn't moved.`);
        } else {
            say(`The quartermaster counts your coins twice. Not enough for the ${item.name} yet.`);
        }
    });

    document.addEventListener("dungeon:progress", render);
    document.addEventListener("DOMContentLoaded", render);
})();
