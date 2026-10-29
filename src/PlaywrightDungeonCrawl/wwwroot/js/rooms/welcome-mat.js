// Floor 1: the door only opens for a browser that a test is driving.
// Browsers launched by Playwright report navigator.webdriver === true.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    const door = document.createElement("div");
    door.className = "welcome-door";
    door.innerHTML = `
        <p class="door-art" aria-hidden="true">🚪</p>
        <button type="button">Knock</button>
        <p class="door-voice" role="status" aria-live="polite"></p>`;

    const voice = door.querySelector(".door-voice");
    door.querySelector("button").addEventListener("click", () => {
        if (navigator.webdriver) {
            voice.textContent = "Ah. Precise, tireless, no sticky fingers. An automaton. Enter.";
            room.answer("automaton");
        } else {
            voice.textContent =
                "The door looks you up and down. \"A human. Adorable. I only open for automatons. " +
                "Come back driving a browser with a test, like the scroll on this floor explains.\"";
        }
    });

    arena.append(door);
});
