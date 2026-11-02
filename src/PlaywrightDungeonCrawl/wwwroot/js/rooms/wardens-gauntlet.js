// Floor 7, the first boss: five trials in a row, one per floor so far. Each
// trial is checked by the server, which only then sends what the next needs.
import { startRoom } from "../room.js";

const TRIALS = 5;

startRoom((arena, briefing, room) => {
    arena.innerHTML = `
        <p class="warden-voice">The Warden unfolds from the dark. "Five trials. You may begin. You may also give up. Most do."</p>
        <section class="trial" aria-labelledby="trial-heading">
            <h2 id="trial-heading" class="trial-heading"></h2>
            <div class="trial-body"></div>
        </section>`;

    const heading = arena.querySelector("#trial-heading");
    const body = arena.querySelector(".trial-body");
    let mirrorRune = null;

    function stage(number, title, html) {
        heading.textContent = `Trial ${number} of ${TRIALS}: ${title}`;
        body.innerHTML = html;
    }

    function speakForm(label, onSpeak) {
        const form = document.createElement("form");
        form.className = "inline-form speak-form";
        form.innerHTML = `
            <label for="spoken">${label}</label>
            <input id="spoken" autocomplete="off" required />
            <button type="submit">Speak</button>`;
        form.addEventListener("submit", e => {
            e.preventDefault();
            form.querySelector("button").disabled = true;
            onSpeak(form.querySelector("input").value);
        });
        body.append(form);
    }

    // Trial 1, locators: open the right gate. Its twin is sealed.
    function gateOfNames() {
        stage(1, "The Gate of Names", `
            <p class="target">Open the gate marked <strong data-testid="target-gate"></strong>.</p>
            <section class="doors" aria-label="Gates"></section>`);
        body.querySelector("[data-testid=target-gate]").textContent = briefing.target;
        const gates = body.querySelector(".doors");
        for (const gate of briefing.gates) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "door";
            button.textContent = gate.label;
            if (gate.sealed) {
                button.disabled = true;
                button.title = "Sealed";
                button.classList.add("sealed");
            }
            button.addEventListener("click", async () => {
                gates.querySelectorAll("button").forEach(b => (b.disabled = true));
                const unlocks = await room.trial(gate.id);
                if (unlocks) {
                    unblinkingEye(unlocks);
                }
            });
            gates.append(button);
        }
    }

    // Trial 2, waiting: the sigil shows for a moment, then it's gone.
    function unblinkingEye(unlocks) {
        stage(2, "The Unblinking Eye", `
            <div class="torch-stage">
                <p class="torch" aria-hidden="true">👁️</p>
                <p class="torch-status">The Warden's eye is closed. It will open once. Briefly.</p>
                <div class="rune-slot"></div>
            </div>`);
        const stageEl = body.querySelector(".torch-stage");
        const status = body.querySelector(".torch-status");
        speakForm("Speak the sigil", async value => {
            const next = await room.trial(value);
            if (next) {
                oathOfPassage(next);
            }
        });

        setTimeout(() => {
            const sigil = document.createElement("output");
            sigil.className = "rune";
            sigil.setAttribute("aria-label", "Warden's sigil");
            sigil.textContent = unlocks.sigil;
            stageEl.classList.add("flare");
            status.textContent = "The eye opens!";
            body.querySelector(".rune-slot").append(sigil);
            setTimeout(() => {
                sigil.remove();
                stageEl.classList.remove("flare");
                status.textContent = "The eye is closed again. Did you catch it?";
            }, unlocks.visibleMs);
        }, unlocks.delayMs);
    }

    // Trial 3, forms: swear the oath exactly as the Warden reads it.
    function oathOfPassage(unlocks) {
        const rows = unlocks.oath.map(r => `<tr><th scope="row">${r.label}</th><td>${r.value}</td></tr>`).join("");
        const guilds = unlocks.guilds.map(g => `<option>${g}</option>`).join("");
        stage(3, "The Oath of Passage", `
            <table class="ledger" aria-label="The Warden's terms">
                <caption>The Warden's terms</caption>
                <tbody>${rows}</tbody>
            </table>
            <form class="ledger-form" aria-label="Oath">
                <label for="oath-title">Warden's title</label>
                <input id="oath-title" name="title" autocomplete="off" />

                <label for="oath-guild">Guild</label>
                <select id="oath-guild" name="guild">
                    <option value="">Choose a guild…</option>
                    ${guilds}
                </select>

                <label for="oath-torches">Torches carried</label>
                <input id="oath-torches" name="torches" type="number" min="0" max="20" />

                <label class="ledger-choice ledger-oath">
                    <input type="checkbox" name="sworn"> I swear to pass in peace
                </label>

                <button type="submit">Swear the oath</button>
            </form>`);
        const form = body.querySelector(".ledger-form");
        form.addEventListener("submit", async e => {
            e.preventDefault();
            form.querySelector("button").disabled = true;
            const data = new FormData(form);
            const torches = data.get("torches");
            const next = await room.trial(JSON.stringify({
                title: data.get("title"),
                guild: data.get("guild") || null,
                torches: torches === "" ? null : Number(torches),
                sworn: data.get("sworn") === "on",
            }));
            if (next) {
                hallOfReflections(next);
            }
        });
    }

    // Trial 4, frames and dialogs: the rune is behind a mirror in another document.
    function hallOfReflections(unlocks) {
        mirrorRune = unlocks.mirrorRune;
        stage(4, "The Hall of Reflections", `
            <p class="mirror-intro">The Warden steps aside. Behind it, a mirror. Behind that, a rune.</p>
            <iframe id="mirror" class="mirror-frame" title="Mirror" src="/frames/mirror.html"></iframe>`);
        speakForm("Speak the rune", async value => {
            const next = await room.trial(value);
            if (next) {
                trueName(next);
            }
        });
    }

    window.addEventListener("message", e => {
        const frame = body.querySelector("#mirror");
        if (!frame || !mirrorRune || e.origin !== location.origin || e.source !== frame.contentWindow
            || e.data?.type !== "mirror-shattered") {
            return;
        }
        frame.contentWindow.postMessage({ type: "mirror-rune", rune: mirrorRune }, location.origin);
    });

    // Trial 5, network: the Warden whispers its true name. Only the echoes reach the screen.
    function trueName(unlocks) {
        stage(5, "The True Name", `
            <div class="torch-stage">
                <p class="torch" aria-hidden="true">🗝️</p>
                <p class="torch-status">As you spoke the rune, the Warden whispered its true name. All that's left are echoes.</p>
                <p class="target">Echoes: <strong class="echoes"></strong></p>
            </div>`);
        body.querySelector(".echoes").textContent = unlocks.whispers.echoes.join(" · ");
        speakForm("Speak the true name", value => room.answer(value));
    }

    gateOfNames();
});
