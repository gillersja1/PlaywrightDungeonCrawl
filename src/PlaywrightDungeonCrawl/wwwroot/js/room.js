// Shared trap-room flow: start an attempt, run the countdown, submit the
// answer and show the outcome. Each room module supplies render().
import { applyCurse } from "./curses.js";

// Failure lines, in the Dungeon AI's voice.
const OUTCOMES = {
    "wrong": "Wrong move. The trap springs. The Dungeon AI has added this to your highlight reel.",
    "too-slow": "Too slow. The trap had time to finish its coffee before you finished.",
    "unknown-attempt": "This attempt is used up. The dungeon doesn't do refunds, but it does do reloads.",
    "cursed-decoy": "You touched a decoy. The curse is delighted. The Dungeon AI is taking notes.",
    "regrown": "The heads grew back. A hydra only dies if every head falls at once: one crawler per head, all at the same time.",
    "lunged": "The head lunged while you hesitated. Strike while its throat is bared.",
    "scarred": "This hand already cut a head, and the hydra remembers. The head grew back. Every head needs a crawler with clean hands.",
    "already-cut": "That head was already off. Each crawler needs a head of their own.",
};

export async function startRoom(render) {
    const roomEl = document.querySelector("[data-room]");
    const slug = roomEl.dataset.room;
    const arena = document.getElementById("arena");
    const result = document.getElementById("room-result");
    const timeLeft = document.getElementById("time-left");

    // Cursed Runs: ?run=<token> plays a run link for its owner; ?curse=<code> is practice.
    const params = new URLSearchParams(location.search);
    const query = params.get("run") ? `?run=${encodeURIComponent(params.get("run"))}`
        : params.get("curse") ? `?curse=${encodeURIComponent(params.get("curse"))}` : "";
    const response = await fetch(`/api/rooms/${slug}/attempts${query}`, { method: "POST" });
    if (!response.ok) {
        arena.innerHTML = `<p><strong>${response.status === 410
            ? "This run link has expired or been used. Start a new cursed run below the room."
            : "That curse doesn't haunt this room."}</strong></p>
            <p><a class="button" href="${location.pathname}">Play the room uncursed</a> <a href="#cursed-runs">Cursed Runs</a></p>`;
        return;
    }
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
            const broken = outcome.curseBroken
                ? `<p class="curse-broken"><strong>💀 ${outcome.curseBroken}: broken.</strong> The badge is yours.</p>`
                : "";
            finish(
                `<p><strong>Room cleared!</strong> The chest creaks open. The Dungeon AI is mildly surprised.${isNew ? ` +${Dungeon.rules.treasure} XP` : ""}</p>
                 ${broken}
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
    if (attempt.curse) {
        const banner = document.createElement("p");
        banner.className = "curse-banner";
        banner.textContent = `💀 ${attempt.curse.name}` +
            (attempt.curse.forKeeps ? `: for keeps, for @${attempt.curse.player}.` : ": practice run, no badge.");
        arena.before(banner);
        if (timeLeft) {
            timeLeft.textContent = (attempt.timeLimitMs / 1000).toFixed(1);
        }
        const limit = roomEl.querySelector(".time-limit");
        if (limit) {
            limit.textContent = `${attempt.timeLimitMs / 1000} seconds`;
        }
    }
    render(arena, attempt.briefing, { answer, trial, reveal, fail, attemptId: attempt.attemptId });
    if (attempt.curse) {
        applyCurse(attempt.curse.code, arena, () => fail("cursed-decoy"));
    }
}
