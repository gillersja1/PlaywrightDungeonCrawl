// Floor 1's scroll: a practice target that, like the Welcome Matt's door,
// only reacts to a browser a test is driving (navigator.webdriver === true).
document.querySelectorAll(".training-dummy").forEach(dummy => {
    const voice = dummy.querySelector(".dummy-voice");
    dummy.querySelector("button").addEventListener("click", () => {
        voice.textContent = navigator.webdriver
            ? "Ow! A clean, tireless poke. You're ready for the door."
            : "The dummy doesn't budge. It only flinches for automatons. Poke it with a test.";
    });
});
