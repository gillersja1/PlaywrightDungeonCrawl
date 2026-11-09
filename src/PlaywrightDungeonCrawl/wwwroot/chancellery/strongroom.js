// The strongroom of floor 13: carry the seal onto the charter (press on the seal, move, let go over the charter).
// Only then does the server seal it, and the sealed charter is a download.
const attempt = new URLSearchParams(location.search).get("attempt");
const base = `/api/rooms/chancellors-seal/attempts/${attempt}`;
const status = document.querySelector(".status");
const seal = document.querySelector(".seal");
const charter = document.querySelector(".charter");
let lifted = false;

seal.addEventListener("pointerdown", e => {
    lifted = true;
    seal.classList.add("lifted");
    e.preventDefault();
});

document.addEventListener("pointerup", async e => {
    if (!lifted) return;
    lifted = false;
    seal.classList.remove("lifted");
    const box = charter.getBoundingClientRect();
    const onCharter = e.clientX >= box.left && e.clientX <= box.right && e.clientY >= box.top && e.clientY <= box.bottom;
    if (!onCharter) { status.textContent = "The seal thuds onto the desk. Wax everywhere, charter untouched."; return; }

    const reply = await fetch(base + "/seal", { method: "POST" });
    const body = await reply.json();
    if (!reply.ok) { status.textContent = body.error; return; }
    charter.classList.add("sealed");
    status.textContent = "The seal bites into the wax. The charter is sealed.";
    document.querySelector(".charter-link").href = body.charter;
    document.querySelector(".collect").hidden = false;
});
