// ChatHistory.cs 的移植。
// 聊天室畫面每次送出就清空，但玩家講過的每一句都留在這裡（鴨子的回應「不」記錄）。
// 網頁版多做一件事：存進 localStorage，重新整理也不會不見。

const KEY = 'debugduck.history';

function load() {
  try {
    const raw = localStorage.getItem(KEY);
    const arr = raw ? JSON.parse(raw) : [];
    return Array.isArray(arr) ? arr : [];
  } catch {
    return [];
  }
}

function save(arr) {
  try {
    localStorage.setItem(KEY, JSON.stringify(arr));
  } catch {
    /* 隱私模式之類的存不了就算了 */
  }
}

export const ChatHistory = {
  /** @returns {{ t: string, text: string }[]} 只讀複本，t 是 ISO 時間字串 */
  entries() {
    return load();
  },

  count() {
    return load().length;
  },

  addPlayerMessage(text) {
    const t = (text ?? '').trim();
    if (!t) return;
    const arr = load();
    arr.push({ t: new Date().toISOString(), text: t });
    save(arr);
  },

  clear() {
    save([]);
  },
};
