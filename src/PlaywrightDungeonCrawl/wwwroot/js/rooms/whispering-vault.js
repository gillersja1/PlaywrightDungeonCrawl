// Floor 6: the combination is only ever in the reveal response. The page fetches
// it as the room opens but never shows it; the echoes on screen are decoys.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    arena.innerHTML = `
        <div class="torch-stage">
            <p class="torch" aria-hidden="true">🔐</p>
            <p class="torch-status">The vault door is sealed. Somewhere inside, something is whispering.</p>
            <p class="target" aria-live="polite">Echoes: <strong class="echoes">…</strong></p>
        </div>
        <form class="inline-form speak-form">
            <label for="spoken-rune">Speak the combination</label>
            <input id="spoken-rune" autocomplete="off" required />
            <button type="submit">Speak</button>
        </form>`;

    room.reveal().then(heard => {
        if (!heard) {
            return;
        }
        // The real combination stays in the response. Only the echoes reach the screen.
        arena.querySelector(".torch-status").textContent =
            "The vault whispered something as you walked in. All that's left are echoes.";
        arena.querySelector(".echoes").textContent = heard.whispers.echoes.join(" · ");
    });

    arena.querySelector(".speak-form").addEventListener("submit", e => {
        e.preventDefault();
        room.answer(arena.querySelector("#spoken-rune").value);
    });
});
