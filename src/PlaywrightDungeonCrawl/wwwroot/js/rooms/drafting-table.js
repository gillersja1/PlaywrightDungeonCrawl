// Floor 15, the Page Object Model: four identical blueprint panels in a shuffled order. The work order says how to
// fill each one; a component object scoped to one panel fills them all.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    const panels = [...briefing.blueprints].sort(() => Math.random() - 0.5);
    const options = briefing.materials.map(m => `<option>${m}</option>`).join("");
    arena.innerHTML = `
        <table class="work-order" aria-label="Work order">
            <thead><tr><th>Blueprint</th><th>Material</th><th>Length (cubits)</th><th>Square corners</th></tr></thead>
            <tbody>${briefing.blueprints.map(b =>
                `<tr><td>${b.name}</td><td>${b.material}</td><td>${b.length}</td><td>${b.square ? "Yes" : "No"}</td></tr>`).join("")}</tbody>
        </table>
        <div class="drafting">
            ${panels.map(b => `
                <section class="blueprint" aria-label="Blueprint: ${b.name}" data-name="${b.name}">
                    <h3>${b.name}</h3>
                    <label>Material <select class="material"><option value="">Choose...</option>${options}</select></label>
                    <label>Length (cubits) <input type="number" class="length" min="1" max="99"></label>
                    <label><input type="checkbox" class="square"> Square corners</label>
                    <button type="button" class="stamp">Stamp</button>
                </section>`).join("")}
        </div>`;

    const stamped = new Map();
    arena.querySelectorAll(".blueprint").forEach(panel => {
        panel.querySelector(".stamp").addEventListener("click", e => {
            e.currentTarget.disabled = true;
            e.currentTarget.textContent = "Stamped";
            panel.querySelectorAll("select, input").forEach(c => c.disabled = true);
            stamped.set(panel.dataset.name, {
                name: panel.dataset.name,
                material: panel.querySelector(".material").value,
                length: Number(panel.querySelector(".length").value),
                square: panel.querySelector(".square").checked,
            });
            if (stamped.size === panels.length) {
                const sorted = [...stamped.values()].sort((a, b) => a.name < b.name ? -1 : a.name > b.name ? 1 : 0);
                room.answer(JSON.stringify(sorted));
            }
        });
    });
});
