// Floor 17, screenshots and visual comparison: six portraits, painted on the server. The forger rehangs all six and
// one comes back a few pixels off. Nothing on the page says which; only comparing pictures tells. The candle
// flickers the whole time, so a screenshot of the whole page never matches the last one.
import { startRoom } from "../room.js";

startRoom(async (arena, briefing, room) => {
    const portraits = `/api/rooms/curators-eye/attempts/${room.attemptId}/portraits`;
    arena.innerHTML = `
        <div class="gallery">
            <span class="candle" aria-hidden="true">🕯️</span>
            <div class="gallery-wall">
                ${briefing.sitters.map((name, i) => `
                    <figure class="portrait">
                        <img alt="Portrait of ${name}" width="120" height="150" data-index="${i}">
                        <figcaption>${name}</figcaption>
                    </figure>`).join("")}
            </div>
            <p class="gallery-status" role="status">The portraits are being hung.</p>
            <form class="accuse">
                <label>The forgery
                    <select name="forgery">
                        <option value="">Choose a portrait</option>
                        ${briefing.sitters.map(name => `<option>${name}</option>`).join("")}
                    </select>
                </label>
                <button type="submit">Accuse</button>
            </form>
        </div>`;
    const status = arena.querySelector(".gallery-status");
    const images = [...arena.querySelectorAll(".portrait img")];

    // The site's content security policy allows data: images (not blob:), so each portrait becomes one.
    const dataUrl = blob => new Promise(resolve => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.readAsDataURL(blob);
    });

    // Fetch all six first, then hang them at once, so no frame is ever half painted or empty.
    async function hang(hanging) {
        const blobs = await Promise.all(images.map(async (_, i) => {
            const reply = await fetch(`${portraits}/${i}?hanging=${hanging}`);
            return reply.ok ? dataUrl(await reply.blob()) : null;
        }));
        if (blobs.includes(null)) {
            return false;
        }
        images.forEach((img, i) => {
            img.src = blobs[i];
        });
        await Promise.all(images.map(img => img.decode()));
        return true;
    }

    arena.querySelector(".accuse").addEventListener("submit", e => {
        e.preventDefault();
        room.answer(e.currentTarget.forgery.value);
    });

    await hang(1);
    status.textContent = "The gallery is quiet. For now.";

    setTimeout(async () => {
        status.textContent = "The lights flicker. Footsteps. Something is being moved.";
        if (await hang(2)) {
            status.textContent = "The lights come back. The forger has been and gone.";
        }
    }, briefing.strikeMs);
});
