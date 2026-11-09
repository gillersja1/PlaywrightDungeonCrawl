// Owl's side of floor 11: sign in as Owl, return Fox's bow, and hear the whisper Fox needs.
const attempt = new URLSearchParams(location.search).get("attempt");
const base = `/api/rooms/masked-ball/attempts/${attempt}`;
const status = document.querySelector(".status");
const post = (path, body) => fetch(base + path, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body ?? {}),
});

document.querySelector(".owl-login").addEventListener("submit", async e => {
    e.preventDefault();
    const reply = await post("/login", { guest: "Owl", password: e.currentTarget.password.value.trim() });
    if (!reply.ok) { status.textContent = reply.status === 404 ? "This ball is over." : (await reply.json()).error; return; }
    status.textContent = "Owl is at the ball. Somewhere, Fox is bowing.";
    document.querySelector(".curtsy").hidden = false;
});

document.querySelector(".curtsy").addEventListener("click", async () => {
    const reply = await post("/curtsy");
    const body = await reply.json();
    if (!reply.ok) { status.textContent = body.error; return; }
    status.textContent = "Owl returns the bow, and leans in to whisper.";
    document.querySelector(".word").textContent = body.whisper;
    document.querySelector(".whisper").hidden = false;
});
