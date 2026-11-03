// The far side of floor 8's portal, opened in a new tab. Both stops read the attempt's hidden data:
// the first shows the toll (a decoy) and moves on; the stall shows the real price.
(async function () {
    const portal = document.querySelector("[data-portal]");
    if (!portal) {
        return;
    }
    const id = portal.dataset.portal;
    const reply = await fetch(`/api/rooms/portal-bazaar/attempts/${id}/reveal`);
    if (!reply.ok) {
        portal.querySelector(".portal-status").textContent =
            "The portal fizzles out. This attempt is over; go back to the room and try again.";
        return;
    }
    const ledger = await reply.json();
    // Under the Curse of the Missing IDs, this tab loses its test ids too.
    const noTestIds = reply.headers.get("X-Dungeon-Curse") === "missing-ids";

    // Each sign is only added once it has something on it, so a test waiting for it gets the real text.
    function write(where, before, testId, value) {
        const strong = document.createElement("strong");
        if (!noTestIds) {
            strong.dataset.testid = testId;
        }
        strong.textContent = value;
        const line = portal.querySelector(where);
        line.append(before, strong);
    }

    if (portal.dataset.stage === "stall") {
        write(".portal-price", "Today's price, in the merchant's own hand: ", "price", ledger.price);
        portal.querySelector(".portal-status").textContent = "The merchant looks up from his ledger.";
        return;
    }

    write(".portal-toll", "A sign on this side reads: toll ", "toll", ledger.toll);
    // A moment on the threshold, then on to the stall. replace(): Back shouldn't return to the swirl.
    setTimeout(() => location.replace(`/portal/${id}/stall`), 700);
})();
