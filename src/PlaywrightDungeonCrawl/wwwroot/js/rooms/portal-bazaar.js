// Floor 8: the price is never on this page. The portal link opens a new tab (/portal/{attempt}),
// which shows a decoy toll and then moves on to the merchant's stall, where the price is written.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    arena.innerHTML = `
        <div class="torch-stage">
            <p class="torch" aria-hidden="true">🌀</p>
            <p class="torch-status">A merchant taps a sign: <em>prices on the other side</em>. Behind him, a portal hums.</p>
            <p><a class="button secondary" href="/portal/${room.attemptId}" target="_blank" rel="noopener">Step through the portal</a></p>
        </div>
        <form class="inline-form pay-form">
            <label for="price-paid">Pay the merchant's price</label>
            <input id="price-paid" autocomplete="off" required />
            <button type="submit">Pay</button>
        </form>`;

    arena.querySelector(".pay-form").addEventListener("submit", e => {
        e.preventDefault();
        room.answer(arena.querySelector("#price-paid").value);
    });
});
