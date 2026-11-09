// Floor 13, the second boss: the Clerk's side of the Chancellery. The Chancellor's password is only in the Registry
// API, the Chancellor signs in from the chambers (with a cookie of the same name as the Clerk's), and the charter
// sealed in the strongroom comes back here as an upload. The server checks every link, in order.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    const base = `/api/rooms/chancellors-seal/attempts/${room.attemptId}`;
    const chambers = `/chancellery/chambers.html?attempt=${room.attemptId}`;
    arena.innerHTML = `
        <div class="chancellery">
            <dl class="registry-notice" aria-label="Registry notice">
                <dt>The Registry</dt><dd><code>${location.origin}${base}/registry</code></dd>
                <dt>Petition number</dt><dd><code class="petition">${briefing.petition}</code></dd>
                <dt>Clerk's password</dt><dd><code class="clerk-password">${briefing.clerkPassword}</code></dd>
            </dl>
            <p class="small">The Registry takes petitions as <code>POST</code> requests, in JSON, naming the office.
                It reads none without the petition number in the <code>X-Petition</code> header. Filed records are
                kept at the address it gives back.</p>
            <p class="chancellery-status" role="status">The Clerk's desk is empty. A sign says: NO CHARTER WITHOUT A CLERK.</p>
            <form class="clerk-login">
                <label>Clerk's password <input name="password" autocomplete="off"></label>
                <button type="submit">Sign in as Clerk</button>
            </form>
            <p class="chambers" hidden>The Chancellor receives visitors in the chambers:
                <a class="chambers-link" href="${chambers}" target="_blank">${location.origin}${chambers}</a></p>
            <form class="file-charter" hidden>
                <label>The sealed charter <input type="file" name="charter" accept=".txt,text/plain"></label>
                <button type="submit">File the charter</button>
            </form>
        </div>`;

    const status = arena.querySelector(".chancellery-status");
    const post = (path, body) => fetch(base + path, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body ?? {}),
    });

    arena.querySelector(".clerk-login").addEventListener("submit", async e => {
        e.preventDefault();
        const reply = await post("/login", { role: "Clerk", password: e.currentTarget.password.value.trim() });
        if (!reply.ok) { status.textContent = (await reply.json()).error; return; }
        status.textContent = "The Clerk is at the desk, quill ready, waiting on the Chancellor.";
        arena.querySelector(".chambers").hidden = false;
        arena.querySelector(".file-charter").hidden = false;
    });

    arena.querySelector(".file-charter").addEventListener("submit", async e => {
        e.preventDefault();
        const file = e.currentTarget.charter.files[0];
        if (!file) { status.textContent = "The Clerk files your empty hands under N for Nothing."; return; }
        const reply = await post("/file", { answer: `${file.name}\n${await file.text()}` });
        const body = await reply.json();
        if (!reply.ok) { status.textContent = body.error; return; }
        status.textContent = `The Clerk files ${file.name}. Somewhere, a bell rings.`;
        room.answer(body.phrase);
    });
});
