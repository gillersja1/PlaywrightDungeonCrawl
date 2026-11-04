// Floor 10, files: the counter hands over a sealed dispatch as a real download, and takes the reply back as an
// upload. The seal is only ever in the file, never on the page, and the reply must have the name the dispatch asks for.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    arena.innerHTML = `
        <div class="counter">
            <p class="counter-status" role="status">A clerk slides a sealed envelope under the grille and taps the sign: NO READING ALOUD.</p>
            <p><a class="button dispatch-link" download
                  href="/api/rooms/couriers-counter/attempts/${room.attemptId}/dispatch">Collect your dispatch</a></p>
            <label class="reply-slot">Your reply
                <input type="file" class="reply-file" accept=".txt,text/plain">
            </label>
            <button type="button" class="hand-over">Hand it over</button>
        </div>`;

    const status = arena.querySelector(".counter-status");
    const input = arena.querySelector(".reply-file");

    arena.querySelector(".dispatch-link").addEventListener("click", () => {
        status.textContent = "The envelope is yours. The clerk pretends not to watch you open it.";
    });

    arena.querySelector(".hand-over").addEventListener("click", async e => {
        const file = input.files[0];
        if (!file) {
            status.textContent = "The clerk stares at your empty hands. The Guild deals in files.";
            return;
        }
        e.currentTarget.disabled = true;
        status.textContent = `The clerk takes ${file.name} with two fingers.`;
        room.answer(`${file.name}\n${await file.text()}`);
    });
});
