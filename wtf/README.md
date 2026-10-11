# WTF: ASCII Protocol

A fast, top-down action shooter made entirely out of moving, colorful ASCII text characters.

---

## What is this game?

In this game, everything you see—your character, the enemies, the ground, the bullets, and the explosions—is built with animated text symbols (like `#`, `@`, `*`, `[ ]`, and `/`). 

You control an operative equipped with an **Eraser Beam** laser on your shoulder. Your mission is to fight smart robot drones, blow off their body parts, pick up those parts, and throw them back as bombs, before entering a portal to fight a giant mechanical boss.

---

## Cool Features

### 1. Shoot Off Enemy Body Parts
Enemies are made of separate parts:
- **Core** (the main body)
- **Chest Armor** (shields their health)
- **Left and Right Arms** (their attack power)
- **Legs** (their movement speed)

When you shoot a part, it breaks off. If you shoot off their legs, they crawl slowly. If you shoot off their arms, they can't attack as easily.

### 2. Throw Enemy Parts as Bombs
When an enemy part breaks off, you automatically pick it up. You can press **Right Click** (or **E** / **Q**) to throw the part like a grenade. When it hits, it blows up into sharp pieces and damages everything nearby.

### 3. Smart Enemies (Powered by Google AI Studio & Gemma)
Enemies are not dumb targets. They watch how you play:
- **Dodge & Dash**: If you aim your laser at them, they quickly dash sideways out of the way.
- **Teleport Behind You**: Sometimes, to save themselves or catch you off guard, they teleport right behind your back!
- **Self-Sacrifice**: If they are low on health, they will blow up their own armor to try and hit you with the blast.

### 4. Giant Boss Fight
Walk into the yellow portal to enter the Boss Arena. There you will face the **Colossal Mech Dreadnought**—a massive boss made of over 500 moving text characters. It fires bullet sprays, shoots rockets, and explodes in a huge chain reaction when defeated.

### 5. Smooth, Clear Screen Health Bars
Your health bar and ammo counter stay steady in the top-left corner, and the boss health bar sits in red in the top-right corner. The bars glide smoothly when taking damage so there is no shaking or screen clipping.

---

## Controls

You can play with either **Keyboard & Mouse** or a **Gamepad Controller**.

| Action | Keyboard & Mouse | Gamepad Controller |
|---|---|---|
| **Move** | `W`, `A`, `S`, `D` or Arrow Keys | Left Thumbstick / D-Pad |
| **Aim** | Move the Mouse | Right Thumbstick |
| **Shoot Laser** | `Left Click` or `Spacebar` | Right Trigger / A Button |
| **Throw Part Bomb** | `Right Click` or `E` or `Q` | Left Trigger / X Button |

---

## How to Add Your Google AI Studio API Key (Optional)

The game works completely fine offline. But if you want enemies to use **Google AI Studio** to plan their moves:

1. Open the file called `google_ai_key.txt` in the game folder.
2. Paste your Google AI Studio API key inside that file.
3. Save the file.
4. That's it! The game will automatically read your key when you start.

*(Note: `google_ai_key.txt` is already added to `.gitignore`, so your key will never be uploaded to GitHub.)*

---

## How to Run the Game

1. Open the project in **Unity** (Unity 6 / 2023+).
2. Go to `Assets/Scenes/`.
3. Open `MainLevel.unity`.
4. Press the **Play** button at the top of the Unity editor.
5. Enjoy the game!
