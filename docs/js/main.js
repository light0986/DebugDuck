// 把所有零件接起來（相當於 App.xaml.cs + PetWindow 的組裝）。

import { DuckBrain } from './duck-brain.js';
import { DuckSound } from './duck-sound.js';
import { Duck } from './duck.js';
import { ChatBubble } from './chat-bubble.js';
import { HistoryView } from './history-view.js';
import { About } from './about.js';

const brain = new DuckBrain();
const history = new HistoryView();
const about = new About();

let duck;

const chat = new ChatBubble({
  brain,
  onReact: (reaction) => duck.react(reaction),
  getDuckRect: () => duck.rect(),
});

duck = new Duck({
  onTalk: () => (chat.visible ? chat.hide() : chat.show()),
  onHistory: () => history.open(),
  onAbout: () => about.open(),
});

// 鴨子移動時，聊天氣泡跟著走
duck.onMove = () => chat.reposition();

// 第一個手勢就把音效解鎖 / 預載
window.addEventListener('pointerdown', () => DuckSound.init(), { once: true });

// 首次到訪的小提示
try {
  if (!localStorage.getItem('debugduck.seen')) {
    localStorage.setItem('debugduck.seen', '1');
    const hint = document.createElement('div');
    hint.className = 'dd-hint';
    hint.textContent = '👉 對右下角的鴨子按右鍵（手機長按）打開選單';
    document.body.appendChild(hint);
    setTimeout(() => hint.classList.add('show'), 400);
    setTimeout(() => hint.remove(), 8000);
  }
} catch { /* ignore */ }
