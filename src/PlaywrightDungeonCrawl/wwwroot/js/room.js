// Shared trap-room flow: start an attempt, run the countdown, submit the
// answer and show the outcome. Each room module supplies render().

// Failure lines, in the Dungeon AI's voice.
const OUTCOMES = {
    "wrong": "Wrong move. The trap springs. The Dungeon AI has added this to your highlight reel.",
    "too-slow": "Too slow. The trap had time to finish its coffee before you finished.",
    "unknown-attempt": "This attempt is used up. The dungeon doesn't do refunds, but it does do reloads.",
};

export async function startRoom(render) {
    const roomEl = document.querySelector("[data-room]");
    const slug = roomEl.dataset.room;
    const arena = document.getElementById("arena");
    const result = document.getElementById("room-result");
    const timeLeft = document.getElementById("time-left");

    const response = await fetch(`/api/rooms/${slug}/attempts`, { method: "POST" });
    const attempt = await response.json();
    const deadline = performance.now() + attempt.timeLimitMs;
    let finished = false;

    const tick = setInterval(() => {
        const left = Math.max(0, deadline - performance.now());
        if (timeLeft) {
            timeLeft.textContent = (left / 1000).toFixed(1);
        }
        if (left === 0) {
            clearInterval(tick);
            roomEl.classList.add("expired");
        }
    }, 100);

    function finish(html, cssClass) {
        finished = true;
        clearInterval(tick);
        result.innerHTML = html;
        result.className = `room-result ${cssClass}`;
        roomEl.classList.add(cssClass);
    }

    function fail(outcome) {
        finish(
            `<p><strong>${OUTCOMES[outcome] ?? "Something went wrong."}</strong></p>
             <p><a class="button" href="">Try again</a> <a href="#nudges">Stuck? Get a nudge</a></p>`,
            "failed");
    }

    // Multi-step rooms: clear the next trial. Resolves to what it unlocks, or
    // null if the trap sprang (the room shows the failure itself).
    async function trial(value) {
        if (finished) {
            return null;
        }
        const reply = await fetch(`/api/rooms/${slug}/attempts/${attempt.attemptId}/trials`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ answer: value }),
        });
        const outcome = await reply.json();
        if (outcome.outcome === "correct") {
            return outcome.unlocks;
        }
        fail(outcome.outcome);
        return null;
    }

    async function answer(value) {
        if (finished) {
            return;
        }
        const reply = await fetch(`/api/rooms/${slug}/attempts/${attempt.attemptId}/answer`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ answer: value }),
        });
        const outcome = await reply.json();
        if (outcome.outcome === "correct") {
            const isNew = Dungeon.mark("treasures", slug);
            finish(
                `<p><strong>Room cleared!</strong> The chest creaks open. The Dungeon AI is mildly surprised.${isNew ? ` +${Dungeon.rules.treasure} XP` : ""}</p>
                 <p>Treasure flag: <code aria-label="Treasure flag" id="treasure-flag">${outcome.flag}</code></p>`,
                "cleared");
        } else {
            fail(outcome.outcome);
        }
    }

    // Lets rooms fetch data the server holds back (e.g. a rune that appears later).
    async function reveal() {
        const reply = await fetch(`/api/rooms/${slug}/attempts/${attempt.attemptId}/reveal`);
        return reply.ok ? reply.json() : null;
    }

    arena.innerHTML = "";
    render(arena, attempt.briefing, { answer, trial, reveal, attemptId: attempt.attemptId });
}
