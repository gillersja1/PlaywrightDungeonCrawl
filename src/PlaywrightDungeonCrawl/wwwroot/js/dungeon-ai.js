// The Dungeon AI: achievement pop-ups, remarks when you take help, and the
// Respawn Room greeting. Polite on the surface, judgmental underneath.
(function () {
    const ACHIEVEMENTS = {
        scrolls: [
            ["Reads the Manual", "Extremely rare among crawlers. The Dungeon AI is almost moved."],
            ["Bookworm", "Another scroll read. Knowledge is power. So is a well-placed locator."],
            ["Scholar of the Deep", "You keep reading things. Suspicious. Admirable. Mostly suspicious."],
        ],
        riddles: [
            ["Riddle Me Done", "Correct answers, in order, on purpose. Noted."],
            ["Smarter Than the Average Crawler", "The bar was on the floor. You cleared it anyway."],
            ["Sphinx Botherer", "Riddles fear you now. Well, they're mildly concerned."],
        ],
        treasures: [
            ["Sticky Fingers", "Treasure claimed. The chest is filing a complaint."],
            ["Automaton Apprentice", "Your script did the work. The Dungeon AI respects delegation."],
            ["Loot Goblin", "Another chest, emptied. The goblins are taking notes."],
        ],
    };

    const HELP_REMARKS = [
        "Help dispensed. The Dungeon AI is not judging you. It is logging you.",
        "A nudge, gladly given. Your score will remember it fondly.",
        "Asking for help is a sign of wisdom. Also of being stuck. Mostly the second one.",
    ];

    const container = document.createElement("div");
    container.className = "ai-toasts";
    container.setAttribute("role", "status");
    container.setAttribute("aria-live", "polite");
    document.addEventListener("DOMContentLoaded", () => document.body.append(container));

    function announce(title, line) {
        const toast = document.createElement("div");
        toast.className = "ai-toast";
        toast.innerHTML = `<p class="ai-label"><span class="ai-eye" aria-hidden="true"></span> Dungeon AI</p>
                           <p class="ai-title"></p><p class="ai-line"></p>`;
        toast.querySelector(".ai-title").textContent = title;
        toast.querySelector(".ai-line").textContent = line;
        container.append(toast);
        setTimeout(() => toast.classList.add("leaving"), 6000);
        setTimeout(() => toast.remove(), 6600);
    }

    function pick(list, count) {
        return list[Math.min(count - 1, list.length - 1)];
    }

    document.addEventListener("dungeon:earned", e => {
        const { kind, progress } = e.detail;
        const [title, line] = pick(ACHIEVEMENTS[kind], progress[kind].length);
        const xp = { scrolls: Dungeon.rules.scroll, riddles: Dungeon.rules.riddle, treasures: Dungeon.rules.treasure }[kind];
        announce(`Achievement: ${title}  +${xp} XP`, line);
    });

    document.addEventListener("dungeon:help", e => {
        const { cost, progress } = e.detail;
        if (cost === 0) {
            announce("Free help", "Floor 1 help is on the house. The Dungeon AI is not a monster. Not on floor 1.");
            return;
        }
        announce(`Help taken  −${cost} XP`, pick(HELP_REMARKS, Object.keys(progress.help).length));
    });

    // The Respawn Room greeting depends on how far you've got.
    document.addEventListener("dungeon:progress", e => {
        const greeting = document.getElementById("ai-greeting");
        if (!greeting) {
            return;
        }
        const { progress, xp, rank } = e.detail;
        const floors = Dungeon.rules.floors;
        const cleared = floors.filter(f => Dungeon.isComplete(f.number, progress)).length;
        const next = floors.find(f => !Dungeon.isComplete(f.number, progress));
        const started = progress.scrolls.length + progress.riddles.length + progress.treasures.length > 0;

        if (!started) {
            greeting.textContent = "New crawler detected. Fresh, optimistic, entirely unprepared. "
                + "Start at floor 1, The Threshold. The Dungeon AI will be watching. It is always watching.";
        } else if (!next) {
            greeting.textContent = `Every open floor, cleared. Score: ${xp}. Rank: ${rank}. `
                + "The Dungeon AI is impressed, and would like that kept between us. Deeper floors are being dug.";
        } else {
            greeting.textContent = `Welcome back, ${rank}. ${cleared} of ${floors.length} floors cleared, score ${xp}. `
                + `Floor ${next.number}, ${next.title}, is waiting. It has been looking forward to this.`;
        }
    });

    window.DungeonAi = { announce };
})();
