// Floor 9, mouse and keyboard: three jobs on the tinkerer's bench, none of which a plain click can do.
// The bolts turn on a double-click, the mechanism's options are on its right-click menu, and the furnace
// dial has no keys and no input box: it's set by pressing on it, moving, and letting go. The server checks
// each job before it hands over the next one's instructions.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    arena.innerHTML = `
        <div class="workbench">
            <p class="workbench-status" role="status">A brass cover hides the furnace's clockwork. Four bolts hold it shut.</p>
            <section class="bench-step bolts-step" aria-label="Cover bolts">
                <div class="bolts"></div>
            </section>
            <section class="bench-step mechanism-step" aria-label="Behind the cover" hidden>
                <p class="tinker-note">Scratched inside the cover: </p>
                <div class="mechanism" aria-label="Clockwork mechanism" role="img"><span aria-hidden="true">⚙️⚙️⚙️</span></div>
                <div class="tinker-menu" role="menu" aria-label="Tinker" hidden></div>
            </section>
            <section class="bench-step furnace-step" aria-label="Furnace" hidden>
                <p class="furnace-target">Set the furnace to </p>
                <div class="dial" role="slider" tabindex="0" aria-label="Furnace temperature"
                     aria-valuemin="0" aria-valuemax="${briefing.dialMax}" aria-valuenow="0">
                    <div class="dial-fill"></div>
                    <div class="dial-thumb"></div>
                </div>
                <p class="dial-reading">The needle reads <strong data-testid="dial-reading">0</strong>°.</p>
                <button type="button" class="fire">Fire the furnace</button>
            </section>
        </div>`;

    const status = arena.querySelector(".workbench-status");

    // Each instruction is only added once it has something on it, so a test reading it gets the real text.
    function write(line, testId, value, after = "") {
        const strong = document.createElement("strong");
        strong.dataset.testid = testId;
        strong.textContent = value;
        line.append(strong, after);
    }

    // Job 1: the bolts. A single click only rattles them; a double-click turns one loose.
    const bolts = arena.querySelector(".bolts");
    let loosened = 0;
    for (let i = 1; i <= briefing.bolts; i++) {
        const bolt = document.createElement("button");
        bolt.type = "button";
        bolt.className = "bolt";
        bolt.setAttribute("aria-label", `Bolt ${i}`);
        bolt.setAttribute("aria-pressed", "false");
        bolt.textContent = "🔩";
        bolt.addEventListener("click", () => {
            if (bolt.getAttribute("aria-pressed") === "false") {
                status.textContent = "The bolt rattles but doesn't turn. It needs a quicker twist.";
            }
        });
        bolt.addEventListener("dblclick", async () => {
            if (bolt.getAttribute("aria-pressed") === "true") {
                return;
            }
            bolt.setAttribute("aria-pressed", "true");
            bolt.classList.add("loose");
            loosened++;
            status.textContent = `Bolt ${i} spins loose. ${briefing.bolts - loosened} to go.`;
            if (loosened === briefing.bolts) {
                const unlocks = await room.trial("loosened");
                if (unlocks) {
                    openCover(unlocks);
                }
            }
        });
        bolts.append(bolt);
    }

    // Job 2: the mechanism. A left click does nothing useful; its options are on the right-click menu.
    function openCover(unlocks) {
        status.textContent = "The cover clatters to the bench. Behind it, the clockwork ticks.";
        arena.querySelector(".bolts-step").hidden = true;
        const step = arena.querySelector(".mechanism-step");
        step.hidden = false;
        write(step.querySelector(".tinker-note"), "tinker-note", unlocks.note);

        const mechanism = step.querySelector(".mechanism");
        const menu = step.querySelector(".tinker-menu");
        for (const option of unlocks.options) {
            const item = document.createElement("button");
            item.type = "button";
            item.setAttribute("role", "menuitem");
            item.textContent = option;
            item.addEventListener("click", async () => {
                menu.querySelectorAll("button").forEach(b => (b.disabled = true));
                menu.hidden = true;
                const next = await room.trial(option);
                if (next) {
                    lightFurnace(next);
                }
            });
            menu.append(item);
        }

        mechanism.addEventListener("click", () => {
            status.textContent = "You poke the gears. They poke back. Tinkerers keep their tools under the other button.";
        });
        mechanism.addEventListener("contextmenu", e => {
            e.preventDefault();
            const box = step.getBoundingClientRect();
            menu.style.left = `${Math.max(0, Math.min(e.clientX - box.left, box.width - 200))}px`;
            menu.style.top = `${e.clientY - box.top}px`;
            menu.hidden = false;
        });
    }

    // Job 3: the furnace dial. No keys, no input box: press on it, move to the spot, let go.
    function lightFurnace(unlocks) {
        status.textContent = "The clockwork purrs. Now the furnace.";
        arena.querySelector(".mechanism-step").hidden = true;
        const step = arena.querySelector(".furnace-step");
        step.hidden = false;
        write(step.querySelector(".furnace-target"), "target-temperature", unlocks.temperature, "°, then fire it.");
        // Bring the furnace to the middle of the screen, so the dial is never tucked under the sticky header.
        step.scrollIntoView({ block: "center" });

        const dial = step.querySelector(".dial");
        const fill = dial.querySelector(".dial-fill");
        const thumb = dial.querySelector(".dial-thumb");
        const reading = step.querySelector("[data-testid=dial-reading]");
        let value = 0;
        let dragging = false;

        function setFrom(clientX) {
            const box = dial.getBoundingClientRect();
            const fraction = Math.min(1, Math.max(0, (clientX - box.left) / box.width));
            value = Math.round((fraction * briefing.dialMax) / briefing.dialStep) * briefing.dialStep;
            const percent = (value / briefing.dialMax) * 100;
            fill.style.width = `${percent}%`;
            thumb.style.left = `${percent}%`;
            dial.setAttribute("aria-valuenow", value);
            reading.textContent = value;
        }

        dial.addEventListener("pointerdown", e => {
            dragging = true;
            dial.setPointerCapture(e.pointerId);
            setFrom(e.clientX);
        });
        dial.addEventListener("pointermove", e => {
            if (dragging) {
                setFrom(e.clientX);
            }
        });
        dial.addEventListener("pointerup", () => (dragging = false));
        dial.addEventListener("keydown", () => {
            status.textContent = "The dial's keyhole rusted shut years ago. It only answers to a steady hand.";
        });

        step.querySelector(".fire").addEventListener("click", e => {
            e.currentTarget.disabled = true;
            room.answer(String(value));
        });
    }
});
