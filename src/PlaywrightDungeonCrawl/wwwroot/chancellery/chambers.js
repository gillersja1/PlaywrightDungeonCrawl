// The Chancellor's side of floor 13: sign in, then open the strongroom, which opens in a new tab.
const attempt = new URLSearchParams(location.search).get("attempt");
const base = `/api/rooms/chancellors-seal/attempts/${attempt}`;
const status = document.querySelector(".status");

document.querySelector(".chancellor-login").addEventListener("submit", async e => {
    e.preventDefault();
    const reply = await fetch(base + "/login", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ role: "Chancellor", password: e.currentTarget.password.value.trim() }),
    });
    if (!reply.ok) { status.textContent = reply.status === 404 ? "The Chancellery is closed." : (await reply.json()).error; return; }
    status.textContent = "The Chancellor stands, sighs, and takes down the strongroom key.";
    document.querySelector(".strongroom-link").href = `strongroom.html?attempt=${encodeURIComponent(attempt)}`;
    document.querySelector(".strongroom").hidden = false;
});
