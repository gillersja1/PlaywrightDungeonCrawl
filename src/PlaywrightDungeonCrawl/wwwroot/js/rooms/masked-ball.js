// Floor 11, login state and browser contexts: Fox signs in here, Owl signs in from the invitation, and both wear a
// cookie named "mask". One context holds one mask, so the room only opens when Fox and Owl are in different contexts.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    const base = `/api/rooms/masked-ball/attempts/${room.attemptId}`;
    const invitation = `/masquerade/invitation.html?attempt=${room.attemptId}`;
    arena.innerHTML = `
        <div class="ball">
            <ul class="guest-list" aria-label="Guest list">
                ${briefing.guests.map(g => `<li><strong>${g.name}</strong>: password <code>${g.password}</code></li>`).join("")}
            </ul>
            <p class="ball-status" role="status">The doorman holds out a gloved hand. Fox's door is this one.</p>
            <form class="fox-login">
                <label>Fox's password <input name="password" autocomplete="off"></label>
                <button type="submit">Enter as Fox</button>
            </form>
            <p class="invite" hidden>Fox bows. Owl's invitation: <a class="invitation-link" href="${invitation}" target="_blank">${location.origin}${invitation}</a></p>
            <form class="unmask" hidden>
                <label>Owl's whisper <input name="whisper" autocomplete="off"></label>
                <button type="submit">Unmask</button>
            </form>
        </div>`;

    const status = arena.querySelector(".ball-status");
    const post = (path, body) => fetch(base + path, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body ?? {}),
    });

    arena.querySelector(".fox-login").addEventListener("submit", async e => {
        e.preventDefault();
        const reply = await post("/login", { guest: "Fox", password: e.currentTarget.password.value.trim() });
        if (!reply.ok) { status.textContent = (await reply.json()).error; return; }
        status.textContent = "Fox is at the ball, mask on, bowing to an empty space where Owl should be.";
        arena.querySelector(".invite").hidden = false;
        arena.querySelector(".unmask").hidden = false;
    });

    arena.querySelector(".unmask").addEventListener("submit", async e => {
        e.preventDefault();
        const reply = await post("/unmask", { answer: e.currentTarget.whisper.value });
        const body = await reply.json();
        if (!reply.ok) { status.textContent = body.error; return; }
        status.textContent = "The host lifts Fox's mask. Everyone pretends to be surprised.";
        room.answer(body.phrase);
    });
});
