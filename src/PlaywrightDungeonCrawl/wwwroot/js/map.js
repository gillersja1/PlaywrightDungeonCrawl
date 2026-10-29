// Dungeon map: progress bar, treasure claims and reset.
(function () {
    const total = document.getElementById("xp-total");
    const rankTitle = document.getElementById("rank-title");
    const bar = document.querySelector(".xp-bar");
    const fill = document.querySelector(".xp-fill");

    document.addEventListener("dungeon:progress", e => {
        const { xp, rank } = e.detail;
        total.textContent = xp;
        document.getElementById("xp-earned").textContent = Dungeon.earned(e.detail.progress);
        document.getElementById("xp-help").textContent = Dungeon.helpSpent(e.detail.progress);
        rankTitle.textContent = rank;
        bar.setAttribute("aria-valuenow", xp);
        fill.style.width = `${Math.min(100, (xp / Dungeon.rules.maxXp) * 100)}%`;
    });

    const form = document.getElementById("claim-form");
    const result = document.getElementById("claim-result");
    form.addEventListener("submit", async e => {
        e.preventDefault();
        const flag = form.flag.value.trim();
        const response = await fetch("/api/treasure/claim", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ flag }),
        });
        const claim = await response.json();
        if (!claim.valid) {
            result.textContent = "That flag doesn't open any chest. Check you copied all of it, braces included.";
            result.className = "bad";
            return;
        }
        if (!Dungeon.isOpen(claim.floor)) {
            result.textContent = `That's treasure from floor ${claim.floor}, which is still sealed for you. Complete floor ${claim.floor - 1} first, then claim it.`;
            result.className = "bad";
            return;
        }
        const isNew = Dungeon.mark("treasures", claim.room);
        result.textContent = isNew
            ? `Treasure claimed: ${claim.treasure} (floor ${claim.floor}). +${Dungeon.rules.treasure} XP`
            : `You already hold the treasure from ${claim.treasure}.`;
        result.className = "good";
        form.reset();
    });

    document.getElementById("reset-progress").addEventListener("click", () => {
        Dungeon.reset();
        result.textContent = "";
    });
})();
