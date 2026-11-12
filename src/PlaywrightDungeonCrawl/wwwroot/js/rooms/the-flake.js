// Floor 19, the final boss: three rounds. Each round the Strike shows up late behind a mist that eats clicks; struck,
// the Flake flickers (its status is replaced, again and again), feints with a false name, then gives its true name;
// and the name box is rebuilt the moment the true name appears. Patience that waits for the right thing wins.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    arena.innerHTML = `
        <div class="flake">
            <p class="flake-round" aria-live="polite"></p>
            <div class="flake-field">
                <div class="flake-mist" aria-hidden="true">~ mist ~</div>
            </div>
            <div class="flake-voice"><p role="status" aria-label="The Flake's voice">The Flake is somewhere in here. Probably.</p></div>
            <form class="banish">
                <span class="name-box"></span>
                <button type="submit">Banish</button>
            </form>
        </div>`;
    const roundEl = arena.querySelector(".flake-round");
    const field = arena.querySelector(".flake-field");
    const mist = arena.querySelector(".flake-mist");
    const voice = arena.querySelector(".flake-voice");
    const box = arena.querySelector(".name-box");
    let round = 1;

    // The status is never edited in place: each change is a brand new element, like a re-rendering framework.
    function say(text) {
        voice.innerHTML = "";
        const p = document.createElement("p");
        p.setAttribute("role", "status");
        p.setAttribute("aria-label", "The Flake's voice");
        p.textContent = text;
        voice.append(p);
    }

    function nameBox() {
        box.innerHTML = `<label>The true name <input name="name" autocomplete="off"></label>`;
    }

    function play(r) {
        roundEl.textContent = `Round ${round} of ${briefing.rounds}`;
        field.querySelector(".strike")?.remove();
        mist.hidden = false;
        nameBox();
        say("The Flake is somewhere in here. Probably.");

        setTimeout(() => {
            const strike = document.createElement("button");
            strike.type = "button";
            strike.className = "strike";
            strike.textContent = "Strike";
            strike.addEventListener("click", () => struck(r), { once: true });
            field.prepend(strike);
        }, r.appearMs);
        setTimeout(() => { mist.hidden = true; }, r.appearMs + r.mistMs);
    }

    function struck(r) {
        field.querySelector(".strike")?.remove();
        const flicker = setInterval(() => say(Math.random() < 0.5 ? "The Flake flickers." : "The Flake flickers..."), 60);
        setTimeout(() => {
            clearInterval(flicker);
            say(`Feint: ${r.feint}`);
            setTimeout(() => {
                say(`True name: ${r.trueName}`);
                nameBox();
            }, r.feintMs);
        }, r.flickerMs);
    }

    arena.querySelector(".banish").addEventListener("submit", async e => {
        e.preventDefault();
        const name = e.currentTarget.querySelector("input")?.value.trim() ?? "";
        if (round < briefing.rounds) {
            const unlocked = await room.trial(name);
            if (unlocked) {
                round++;
                play(unlocked.next);
            }
        } else {
            room.answer(name);
        }
    });

    play(briefing.first);
});
