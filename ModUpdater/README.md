# ModUpdater for Minecraft

[English](#english) | [Українська](#українська)

---
<a id="english"></a>
## English

### Minecraft Mod Updater

This tool helps you automatically update your Minecraft Java Edition mods to a new version. It scans your existing mods folder and attempts to find and download the correct versions for your desired Minecraft version and mod loader (Fabric or Forge).

### How it Works

1.  **Scan Local Mods**: The program first looks inside each `.jar` file to find the mod's unique ID (from `fabric.mod.json` or `mods.toml`). If that fails, it uses a cleaned-up version of the filename as a search query.
2.  **Check by Hash**: It calculates a unique hash (SHA1) for each mod file and asks Modrinth if it recognizes that exact file. This is the most reliable way to identify a mod.
3.  **Search by Name**: If the hash is not found, the tool searches for the mod by its name on Modrinth. It uses an improved search logic to pick the most likely candidate from the top 5 results.
4.  **Download from Modrinth**: If a matching project is found on Modrinth, the tool looks for a version compatible with your target Minecraft version and loader.
    *   If an exact match is found, it's downloaded and marked as "OK".
    *   If no exact match is found, it downloads the *latest available version* for that loader and flags it as **potentially mismatched**. You will need to check this manually.
5.  **Search on GitHub**: If the mod isn't found on Modrinth, the tool performs a fallback search on GitHub. It downloads the latest release from the most popular repository matching the mod's name. These downloads are always flagged for manual checking.
6.  **Report Failures**: Any mods that cannot be found on either Modrinth or GitHub are listed at the end. The tool will then offer to open a search page for each of them on CurseForge in your browser.

### How to Use

1.  Go to the **[Releases](https://github.com/YOUR-USERNAME/YOUR-REPOSITORY/releases)** page on GitHub and download the `ModUpdater.exe` file from the latest release.
2.  Run `ModUpdater.exe`.
3.  **Enter path to mods folder**: Paste the full path to the folder where your current mods are located and press Enter.
4.  **Enter Minecraft version**: Type the version you want to update to (e.g., `1.21.1`).
5.  **Enter mod loader**: Type `fabric` or `forge`.
6.  The tool will start processing your mods. A new folder named `Mods_[version]_[loader]` will be created on your Desktop, containing the newly downloaded mods.

### Important Notes

*   **Not a 100% Guarantee**: This tool automates a complex process, but it might not find every single mod, especially if they are old, obscure, or exclusively available on CurseForge.
*   **Check Mismatched Mods**: Pay close attention to the final warning message. It will list all mods that were downloaded from GitHub or didn't have a perfect version match on Modrinth. You should verify these `.jar` files to ensure they are compatible with your game version before playing.
*   **CurseForge**: This tool does not download files from CurseForge automatically due to their API policies. However, it helps you by opening search pages for any mods it couldn't find.
*   **Safety**: The application interacts with the official APIs from Modrinth and GitHub. It **does not** modify your existing mods folder; it only creates a new one on your Desktop.

---
<a id="українська"></a>
## Українська

### Minecraft Mod Updater

Цей інструмент допоможе вам автоматично оновити ваші моди для Minecraft Java Edition до нової версії. Він сканує вашу наявну папку з модами та намагається знайти й завантажити правильні версії для бажаної версії Minecraft та завантажувача модів (Fabric або Forge).

### Як це працює

1.  **Сканування локальних модів**: Програма спочатку перевіряє кожен `.jar` файл, щоб знайти унікальний ідентифікатор мода (з `fabric.mod.json` або `mods.toml`). Якщо це не вдається, вона використовує очищену назву файлу для пошуку.
2.  **Перевірка за хешем**: Інструмент обчислює унікальний хеш (SHA1) для кожного файлу мода і запитує Modrinth, чи відомий цей файл. Це найнадійніший спосіб ідентифікації мода.
3.  **Пошук за назвою**: Якщо хеш не знайдено, програма шукає мод за його назвою на Modrinth. Вона використовує покращену логіку пошуку, щоб вибрати найбільш імовірного кандидата з топ-5 результатів.
4.  **Завантаження з Modrinth**: Якщо відповідний проєкт знайдено на Modrinth, інструмент шукає версію, сумісну з вашою цільовою версією Minecraft та завантажувачем.
    *   Якщо знайдено точну відповідність, мод завантажується з позначкою "OK".
    *   Якщо точної відповідності немає, завантажується *остання доступна версія* для цього завантажувача і позначається як **ймовірно несумісна**. Вам потрібно буде перевірити її вручну.
5.  **Пошук на GitHub**: Якщо мод не знайдено на Modrinth, інструмент виконує резервний пошук на GitHub. Він завантажує останній реліз із найпопулярнішого репозиторію, що відповідає назві мода. Такі завантаження завжди позначаються для ручної перевірки.
6.  **Звіт про невдачі**: Моди, які не вдалося знайти ні на Modrinth, ні на GitHub, перераховуються в кінці. Після цього програма запропонує відкрити сторінку пошуку для кожного з них на CurseForge у вашому браузері.

### Як користуватися

1.  Перейдіть на сторінку **[Releases](https://github.com/YOUR-USERNAME/YOUR-REPOSITORY/releases)** на GitHub і завантажте файл `ModUpdater.exe` з останнього релізу.
2.  Запустіть `ModUpdater.exe`.
3.  **Enter path to mods folder**: Вставте повний шлях до папки, де знаходяться ваші поточні моди, і натисніть Enter.
4.  **Enter Minecraft version**: Введіть версію, до якої ви хочете оновитися (наприклад, `1.21.1`).
5.  **Enter mod loader**: Введіть `fabric` або `forge`.
6.  Інструмент почне обробку ваших модів. Нова папка з назвою `Mods_[версія]_[завантажувач]` буде створена на вашому робочому столі, і в ній будуть міститися щойно завантажені моди.

### Важливі примітки

*   **Без 100% гарантії**: Цей інструмент автоматизує складний процес, але він може не знайти абсолютно всі моди, особливо якщо вони старі, маловідомі або доступні виключно на CurseForge.
*   **Перевіряйте несумісні моди**: Зверніть особливу увагу на фінальне попередження. У ньому буде список усіх модів, завантажених з GitHub або тих, що не мали ідеальної відповідності версії на Modrinth. Ви повинні перевірити ці `.jar` файли, щоб переконатися, що вони сумісні з вашою версією гри, перш ніж грати.
*   **CurseForge**: Цей інструмент не завантажує файли з CurseForge автоматично через політику їхнього API. Однак він допоможе вам, відкривши сторінки пошуку для будь-яких модів, які не вдалося знайти.
*   **Безпека**: Програма взаємодіє з офіційними API Modrinth та GitHub. Вона **не змінює** вашу наявну папку з модами; вона лише створює нову на вашому робочому столі.
