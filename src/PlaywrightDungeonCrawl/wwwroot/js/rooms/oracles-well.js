// Floor 12, API testing: the page has no way to ask the Oracle anything. The notice documents the API; the test has
// to call it, follow the 201's Location, and bring the prophecy back to the page.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    const questions = `${location.origin}/api/rooms/oracles-well/attempts/${room.attemptId}/questions`;
    arena.innerHTML = `
        <div class="well">
            <section class="well-notice" aria-label="Notice by the well">
                <p>The Oracle accepts questions by post only.</p>
                <pre><code>POST <span class="questions-url">${questions}</span>
X-Offering: <span class="offering">${briefing.offering}</span>
Content-Type: application/json

{ "question": "..." }</code></pre>
                <p>Answers are kept where the Oracle says they are. Bring the offering when you collect.</p>
            </section>
            <p class="well-status" role="status">The water is still. Somewhere below, something is waiting for a request.</p>
            <form class="speak">
                <label>The prophecy <input name="prophecy" autocomplete="off"></label>
                <button type="submit">Speak it</button>
            </form>
        </div>`;

    arena.querySelector(".speak").addEventListener("submit", e => {
        e.preventDefault();
        arena.querySelector(".well-status").textContent = "You speak the prophecy into the well. It echoes back, satisfied.";
        room.answer(e.currentTarget.prophecy.value.trim());
    });
});
