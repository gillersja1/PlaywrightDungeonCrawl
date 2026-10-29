// Floor 3: a rune flares into view at a random moment, for less than a
// second, then is gone from the page entirely.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    arena.innerHTML = `
        <div class="torch-stage">
            <p class="torch" aria-hidden="true">🕯️</p>
            <p class="torch-status">The torch gutters. Watch closely.</p>
            <div class="rune-slot"></div>
        </div>
        <form class="inline-form speak-form">
            <label for="spoken-rune">Speak the rune</label>
            <input id="spoken-rune" autocomplete="off" required />
            <button type="submit">Speak</button>
        </form>`;

    const stage = arena.querySelector(".torch-stage");
    const status = arena.querySelector(".torch-status");
    const slot = arena.querySelector(".rune-slot");

    setTimeout(async () => {
        const revealed = await room.reveal();
        if (!revealed) {
            return;
        }
        const rune = document.createElement("output");
        rune.className = "rune";
        rune.setAttribute("aria-label", "Torch rune");
        rune.textContent = revealed.rune;
        stage.classList.add("flare");
        status.textContent = "The torch flares!";
        slot.append(rune);

        setTimeout(() => {
            rune.remove();
            stage.classList.remove("flare");
            status.textContent = "Darkness again. Did you catch it?";
        }, briefing.visibleMs);
    }, briefing.delayMs);

    arena.querySelector(".speak-form").addEventListener("submit", e => {
        e.preventDefault();
        room.answer(arena.querySelector("#spoken-rune").value);
    });
});
