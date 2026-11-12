// Floor 18, emulation and the clock: the door names tonight's traveler. The page reads the browser (time zone,
// language, position, touch, width) when the bell strikes midnight there, and the doorkeeper checks the readings.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    arena.innerHTML = `
        <div class="belfry">
            <section class="belfry-door" aria-label="Notice on the Belfry door">
                <p>Tonight the door opens for <strong>a traveler from ${briefing.city}</strong>:</p>
                <ul>
                    <li>on a phone,</li>
                    <li>keeping <code class="time-zone">${briefing.timeZone}</code> time,</li>
                    <li>speaking ${briefing.language} (<code class="locale">${briefing.locale}</code>),</li>
                    <li>standing in ${briefing.city} itself
                        (<span class="latitude">${briefing.latitude}</span>, <span class="longitude">${briefing.longitude}</span>),</li>
                    <li>when the bell strikes midnight there.</li>
                </ul>
            </section>
            <p class="belfry-clock" aria-label="Tower clock">--:--:--</p>
            <p class="belfry-status" role="status">The bell hangs still, waiting for midnight.</p>
        </div>`;
    const clock = arena.querySelector(".belfry-clock");
    const status = arena.querySelector(".belfry-status");

    const show = () => {
        clock.textContent = new Date().toLocaleTimeString("en-GB", { hour12: false });
    };
    show();
    setInterval(show, 1000);

    // Milliseconds until the next local midnight, in whatever time zone the browser keeps.
    const now = new Date();
    const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
    setTimeout(ring, midnight - now);

    function position() {
        return new Promise(resolve => {
            if (!navigator.geolocation) {
                resolve(null);
                return;
            }
            navigator.geolocation.getCurrentPosition(p => resolve(p.coords), () => resolve(null), { timeout: 3000 });
        });
    }

    async function ring() {
        const epoch = Date.now();
        status.textContent = "BONG. The bell strikes midnight. The doorkeeper looks you over.";
        const coords = await position();
        const readings = {
            timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
            language: navigator.language,
            latitude: coords?.latitude ?? null,
            longitude: coords?.longitude ?? null,
            touch: navigator.maxTouchPoints > 0,
            width: window.innerWidth,
            epoch,
        };
        const papers = await fetch(`/api/rooms/belfry/attempts/${room.attemptId}/papers`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(readings),
        });
        const verdict = await papers.json().catch(() => ({ problems: ["The doorkeeper is busy."] }));
        if (verdict.problems?.length) {
            status.textContent = `The doorkeeper turns you away. ${verdict.problems.join(" ")}`;
            room.fail("turned-away");
            return;
        }
        const unlocked = await room.trial(JSON.stringify(readings));
        if (unlocked) {
            status.textContent = `The door swings open. The bell whispers its word: ${unlocked.word}.`;
            room.answer(unlocked.word);
        }
    }
});
