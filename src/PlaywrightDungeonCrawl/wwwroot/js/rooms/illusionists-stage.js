// Floor 14, mocking responses: as soon as the room opens, the stage fetches its act and a heckler. The real act has
// the curtain down and the real heckler always turns up, so only a test that changes the replies sees the show.
import { startRoom } from "../room.js";

startRoom(async (arena, briefing, room) => {
    const base = `/api/rooms/illusionists-stage/attempts/${room.attemptId}`;
    arena.innerHTML = `
        <div class="stage">
            <p class="stage-status" role="status">The house lights dim. Somewhere backstage, a request is on its way.</p>
        </div>`;
    const status = arena.querySelector(".stage-status");

    const [act, heckler] = await Promise.allSettled([
        fetch(`${base}/act`).then(r => r.json()),
        fetch(`${base}/heckler`).then(r => r.ok ? r.json() : null),
    ]);

    if (heckler.status === "fulfilled" && heckler.value) {
        status.textContent = `A voice from the back row: "${heckler.value.heckle}" The illusionist storms off.`;
        return;
    }
    if (act.status !== "fulfilled" || act.value.curtain !== "up") {
        status.textContent = "The curtain stays down. The illusionist refuses to perform behind it.";
        return;
    }
    status.textContent = `The curtain rises. The illusionist pulls the word "${act.value.word}" out of thin air.`;
    room.answer(act.value.word);
});
