// Floor 1: forty doors, two with the target sign, one of those sealed.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    const sign = document.createElement("p");
    sign.className = "target";
    sign.innerHTML = `Open the door marked <strong data-testid="target-sign"></strong>.`;
    sign.querySelector("strong").textContent = briefing.target;

    const hall = document.createElement("section");
    hall.className = "doors";
    hall.setAttribute("aria-label", "Hall");

    for (const door of briefing.doors) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "door";
        button.textContent = door.label;
        if (door.sealed) {
            button.disabled = true;
            button.title = "Sealed";
            button.classList.add("sealed");
        }
        button.addEventListener("click", () => room.answer(door.id));
        hall.append(button);
    }

    arena.append(sign, hall);
});
