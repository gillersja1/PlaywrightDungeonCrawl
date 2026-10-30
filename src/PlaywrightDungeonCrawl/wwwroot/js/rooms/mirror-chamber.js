// Floor 5: the rune is behind a mirror in an iframe, inside a shadow root, and
// shattering it asks for confirmation first.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    arena.innerHTML = `
        <p class="mirror-intro">A tall mirror fills the next room. Your reflection waits behind the glass.</p>
        <iframe id="mirror" class="mirror-frame" title="Mirror" src="/frames/mirror.html"></iframe>
        <form class="inline-form speak-form">
            <label for="spoken-rune">Speak the rune</label>
            <input id="spoken-rune" autocomplete="off" required />
            <button type="submit">Speak</button>
        </form>`;

    const frame = arena.querySelector("#mirror");

    // The mirror asks for the rune once it's shattered; the page holds the attempt.
    window.addEventListener("message", async e => {
        if (e.origin !== location.origin || e.source !== frame.contentWindow || e.data?.type !== "mirror-shattered") {
            return;
        }
        const revealed = await room.reveal();
        if (revealed) {
            frame.contentWindow.postMessage({ type: "mirror-rune", rune: revealed.rune }, location.origin);
        }
    });

    arena.querySelector(".speak-form").addEventListener("submit", e => {
        e.preventDefault();
        room.answer(arena.querySelector("#spoken-rune").value);
    });
});
