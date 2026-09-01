# DebugDuck 🐤

桌面上的除錯小黃鴨 —— 把你的 bug 一行一行講給牠聽。

[![線上試玩](https://img.shields.io/badge/線上試玩-網頁版-ffd400?style=for-the-badge)](https://light0986.github.io/DebugDuck/)
[![Download](https://img.shields.io/github/v/release/light0986/DebugDuck?label=桌面版下載&sort=semver&style=for-the-badge)](https://github.com/light0986/DebugDuck/releases/latest)

**小黃鴨除錯法（Rubber Duck Debugging）** 是個經典技巧:對著橡皮鴨一行行解釋你的程式碼,
講到一半常常自己就發現問題在哪。DebugDuck 把那隻鴨子放到你的桌面上。

<!-- 之後可以放一張截圖或操作 GIF -->

---

## 🕹️ 線上試玩(免安裝)

**<https://light0986.github.io/DebugDuck/>**

瀏覽器直接開就能玩,手機也可以 —— 不用下載、不用安裝。

- 畫面中下方一隻小鴨,會輕輕上下起伏
- **點一下**小鴨,牠會 Q 彈壓扁一下 + 叫一聲
- 上方的輸入框打字送出 → 小鴨思考 3 秒 → 隨機回你一句(`嘎` / `嘎嘎` / `嘎嘎嘎` / `?` / `!`),字數越多叫越多聲
- 對話會存在瀏覽器裡,每則訊息附上 `HH:mm` 時間,可以往上捲看歷史

> 網頁版是單一 HTML 檔([`Web/DebugDuck.html`](Web/DebugDuck.html)),圖片和音效都用 base64 內嵌,沒有任何外部相依。

---

## 下載(Windows 桌面版)

想要一直待在桌面上的桌寵版,到 **[Releases](https://github.com/light0986/DebugDuck/releases/latest)**
下載最新版的 `DebugDuck-vX.Y.Z.zip`,解壓縮後直接執行 `DebugDuck.exe`。

**系統需求**

- Windows 10 / 11
- .NET Framework 4.7.2(現代 Windows 都內建,通常不用另外裝)

> ⚠️ 程式沒有數位簽章,第一次執行 Windows 會跳「Windows 已保護您的電腦」。
> 點 **其他資訊 → 仍要執行** 即可。

---

## 操作說明(桌面版)

| 操作 | 行為 |
| --- | --- |
| 左鍵拖曳小鴨 | 移動位置(會記住,下次開在原位) |
| 左鍵點一下小鴨 | 牠會 Q 彈壓扁一下 + 叫一聲 |
| **右鍵**點小鴨 | 開選單:跟鴨子說話 / 查看歷史訊息 / 回到畫面中央 / 關於 / 結束 |

**跟鴨子說話**:打字送出後,小鴨思考 3 秒,然後隨機回你一句(`嘎` / `嘎嘎` / `嘎嘎嘎` / `?` / `!`)。
牠不會幫你解 bug —— 重點是「講出來」的過程你自己會抓到。

你送出的訊息會存進背景紀錄,「查看歷史訊息」可以看完整對話(以時間戳記斷句)。

---

## 功能

- 透明無邊框、永遠置頂的桌寵,不佔工作列
- 逐格動畫支援(把 PNG 放到 exe 旁的 `Assets/<狀態>/` 資料夾即可自訂),預設是內嵌的靜態黃小鴨
- 叫聲:內嵌音效,連叫時用多個播放器做重疊混音
- 小鴨圖示、聊天室、關於視窗

---

## 從原始碼建置

需要 Visual Studio 2022 以上(含「.NET 桌面開發」工作負載)。

```
git clone https://github.com/light0986/DebugDuck.git
```

用 Visual Studio 開 `DebugDuck.slnx`,按 <kbd>F5</kbd> 執行。

- 語言 / 框架:C# / WPF / .NET Framework 4.7.2
- 無 NuGet 相依,圖片與音效都內嵌為組件資源

**網頁版**([`Web/DebugDuck.html`](Web/DebugDuck.html)):單一 HTML 檔,沒有建置步驟。
由 GitHub Pages 從 `main` 分支根目錄託管(根目錄的 `index.html` 會轉址過去)。

---

## 授權

<!-- 還沒指定授權，建議加一個（例如 MIT）。在 GitHub repo 頁按 Add file → Create new file → 檔名打 LICENSE，會有樣板可選。 -->
尚未指定。
