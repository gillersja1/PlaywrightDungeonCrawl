// Floor 4: copy the ledger into a registration form of mixed input types
// (text, select, radio, number, date, checkbox) before the scribe loses patience.
import { startRoom } from "../room.js";

startRoom((arena, briefing, room) => {
    const id = label => "ledger-" + label.toLowerCase().replace(/[^a-z]+/g, "-");
    const rows = briefing.ledger
        .map(row => `<tr><th scope="row">${row.label}</th><td>${row.value}</td></tr>`)
        .join("");
    const guilds = briefing.guilds.map(g => `<option>${g}</option>`).join("");
    const classes = briefing.classes
        .map(c => `<label class="ledger-choice"><input type="radio" name="class" value="${c}"> ${c}</label>`)
        .join("");

    arena.innerHTML = `
        <div class="scribe">
            <p class="scribe-voice">The scribe taps a quill. "Copy it exactly. I haven't got all day. I've got six seconds."</p>
            <table class="ledger" aria-label="Ledger">
                <caption>The ledger</caption>
                <tbody>${rows}</tbody>
            </table>
        </div>
        <form class="ledger-form" aria-label="Registration">
            <label for="${id("Adventurer name")}">Adventurer name</label>
            <input id="${id("Adventurer name")}" name="name" autocomplete="off" />

            <label for="${id("Guild")}">Guild</label>
            <select id="${id("Guild")}" name="guild">
                <option value="">Choose a guild…</option>
                ${guilds}
            </select>

            <fieldset>
                <legend>Class</legend>
                ${classes}
            </fieldset>

            <label for="${id("Torches carried")}">Torches carried</label>
            <input id="${id("Torches carried")}" name="torches" type="number" min="0" max="20" />

            <label for="${id("Departure date")}">Departure date</label>
            <input id="${id("Departure date")}" name="departure" type="date" />

            <label class="ledger-choice ledger-oath">
                <input type="checkbox" name="oath"> I swear this registration is true
            </label>

            <button type="submit">Sign the ledger</button>
        </form>`;

    const form = arena.querySelector(".ledger-form");
    form.addEventListener("submit", e => {
        e.preventDefault();
        const data = new FormData(form);
        const torches = data.get("torches");
        room.answer(JSON.stringify({
            name: data.get("name"),
            guild: data.get("guild") || null,
            class: data.get("class"),
            torches: torches === "" ? null : Number(torches),
            departure: data.get("departure") || null,
            oath: data.get("oath") === "on",
        }));
    });
});
