// Floor 16, fixtures, setup and parallel tests: one setup call starts a hunt; each head is faced from its own page,
// in its own browser context; the heads only rear once all five crawlers are there together, and all five have to
// fall within a few seconds.
import { startRoom } from "../room.js";

startRoom(async (arena, briefing, room) => {
    const params = new URLSearchParams(location.search);
    const hunt = params.get("hunt");
    const head = Number(params.get("head"));
    const hunts = `${location.origin}/api/rooms/severing-pit/hunts`;
    const seconds = ms => ms / 1000;

    if (!/^[0-9a-f-]{36}$/i.test(hunt ?? "") || !(head >= 1 && head <= briefing.heads)) {
        arena.innerHTML = `
            <div class="pit">
                <section class="pit-notice" aria-label="Notice at the edge of the pit">
                    <p>Five heads. They only rear when a crawler stands at every one, and if they don't all fall
                       within ${seconds(briefing.windowMs)} seconds, they grow back.</p>
                    <p>Start a hunt first. It answers with a <code>huntId</code>:</p>
                    <pre><code>POST <span class="hunts-url">${hunts}</span></code></pre>
                    <p>Then send one crawler for each head, 1 to ${briefing.heads}, all at once:</p>
                    <pre><code>${location.origin}${location.pathname}?hunt=<var>huntId</var>&amp;head=<var>1</var></code></pre>
                    <p>A crawler can only face one head. The hydra won't wait more than ${seconds(briefing.patienceMs)} seconds for the rest.</p>
                </section>
                <p class="pit-status" role="status">Below, five heads sway in the dark, waiting for a hunt.</p>
            </div>`;
        return;
    }

    arena.innerHTML = `
        <div class="pit">
            <p class="pit-head coiled" aria-label="Head ${head}">🐍 Head ${head} of ${briefing.heads}</p>
            <p class="pit-status" role="status">You face head ${head}. It's coiled, waiting to see who else turns up.</p>
            <button type="button" class="cut" disabled>Cut</button>
        </div>`;
    const status = arena.querySelector(".pit-status");
    const headEl = arena.querySelector(".pit-head");
    const cut = arena.querySelector(".cut");
    const post = (path) => fetch(`${hunts}/${hunt}/heads/${head}/${path}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ attempt: room.attemptId }),
    });

    const faced = await post("face");
    if (!faced.ok) {
        room.fail((await faced.json().catch(() => ({}))).outcome ?? "regrown");
        return;
    }

    let rearing = false;
    let struck = false;
    const poll = setInterval(async () => {
        const state = await (await fetch(`${hunts}/${hunt}?attempt=${room.attemptId}`)).json();
        if (state.state === "slain") {
            clearInterval(poll);
            status.textContent = "All five heads are down. The hydra falls, and leaves a fang behind.";
            room.answer(state.fang);
        } else if (state.state === "regrown") {
            clearInterval(poll);
            cut.disabled = true;
            room.fail("regrown");
        } else if (state.state === "rearing" && !rearing) {
            rearing = true;
            setTimeout(rear, briefing.rearMs);
        } else if (!struck && !rearing) {
            status.textContent = `You face head ${head}. ${state.faced} of ${state.heads} crawlers are at the pit.`;
        } else if (struck) {
            status.textContent = `Head ${head} is down. ${state.fallen} of ${state.heads} down. Hold the line.`;
        }
    }, 200);

    function rear() {
        headEl.classList.replace("coiled", "reared");
        status.textContent = `Every head is faced. Head ${head} rears up and bares its throat. Now!`;
        // It stays bared until the hunt ends: the server's window decides whether the cuts came together.
        cut.disabled = false;
    }

    cut.addEventListener("click", async () => {
        if (struck) {
            return;
        }
        struck = true;
        cut.disabled = true;
        const reply = await post("cut");
        if (!reply.ok) {
            clearInterval(poll);
            room.fail((await reply.json().catch(() => ({}))).outcome ?? "regrown");
            return;
        }
        headEl.classList.replace("reared", "severed");
        status.textContent = `Head ${head} falls. ${(await reply.json()).fallen} of ${briefing.heads} down.`;
    });
});
