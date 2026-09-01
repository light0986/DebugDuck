// ChatBubbleWindow.xaml(.cs) 的移植：貼在鴨子上方的聊天氣泡。
// 一次只顯示兩則（玩家 + 鴨子）。送出 → 思考 3 秒 → 隨機回應。思考期間鎖住輸入。
// 點氣泡以外的地方會關閉（剛開的 300ms 內忽略）。

import { ChatHistory } from './chat-history.js';
import { DuckSound } from './duck-sound.js';

const THINK_MS = 3000;
const BUBBLE_WIDTH = 300;

export class ChatBubble {
  /** @param {{ brain: import('./duck-brain.js').DuckBrain, onReact: (r:string)=>void, getDuckRect: ()=>DOMRect }} opts */
  constructor(opts) {
    this.brain = opts.brain;
    this.onReact = opts.onReact;
    this.getDuckRect = opts.getDuckRect;

    this.busy = false;
    this.greeted = false;
    this.openedAt = 0;
    this._thinkTimer = null;
    this._duckMsgEl = null;

    this.#buildDom();
    this.#wire();
  }

  #buildDom() {
    const el = document.createElement('div');
    el.className = 'dd-bubble';
    el.hidden = true;
    el.style.width = BUBBLE_WIDTH + 'px';
    el.innerHTML =
      '<div class="dd-log"></div>' +
      '<div class="dd-input">' +
      '  <textarea rows="1" placeholder="把你的 bug 講給鴨子聽…"></textarea>' +
      '  <button type="button">說</button>' +
      '</div>';
    document.body.appendChild(el);

    this.el = el;
    this.logEl = el.querySelector('.dd-log');
    this.inputAreaEl = el.querySelector('.dd-input');
    this.inputEl = el.querySelector('textarea');
    this.sendBtn = el.querySelector('button');
  }

  #wire() {
    this.sendBtn.addEventListener('click', () => this.#send());

    this.inputEl.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        this.#send();
      } else if (e.key === 'Escape') {
        e.preventDefault();
        this.hide();
      }
    });

    // 點氣泡以外的地方 → 關閉
    document.addEventListener('pointerdown', (e) => {
      if (this.el.hidden) return;
      if (Date.now() - this.openedAt < 300) return;
      if (!this.el.contains(e.target)) this.hide();
    });

    window.addEventListener('resize', () => this.reposition());
  }

  // ---------- 顯示 / 位置 ----------

  show() {
    this.openedAt = Date.now();
    this.el.hidden = false;

    if (!this.greeted) {
      this.greeted = true;
      this.#addBubble('嘎', 'duck');
    }
    this.reposition();
    if (!this.busy) this.inputEl.focus();
  }

  hide() {
    this.el.hidden = true;
  }

  get visible() { return !this.el.hidden; }

  reposition() {
    if (this.el.hidden) return;
    const d = this.getDuckRect();
    const b = this.el.getBoundingClientRect();

    let left = d.left + d.width / 2 - b.width / 2;
    left = Math.max(6, Math.min(left, window.innerWidth - b.width - 6));

    const above = d.top - b.height + 16;
    const top = above >= 6 ? above : d.top + d.height - 16;

    this.el.style.left = left + 'px';
    this.el.style.top = top + 'px';
  }

  // ---------- 送出 ----------

  #send() {
    if (this.busy) return;
    const text = this.inputEl.value.trim();
    if (!text) return;

    this.inputEl.value = '';

    // 只存玩家訊息，然後清空畫面
    ChatHistory.addPlayerMessage(text);
    this.logEl.replaceChildren();

    this.#addBubble(text, 'player');             // 1) 玩家（淡綠）
    this._duckMsgEl = this.#addBubble('思考中..', 'duck'); // 2) 鴨子「思考中..」（淡藍）

    this.#setBusy(true);
    const reply = this.brain.respond(text);

    this._thinkTimer = setTimeout(() => {
      this._thinkTimer = null;
      this._duckMsgEl.textContent = reply.text;
      DuckSound.speak(reply.text);               // 台詞出現的同時叫
      this.onReact?.(reply.reaction);
      this.#setBusy(false);
      this.reposition();
    }, THINK_MS);
  }

  #setBusy(busy) {
    this.busy = busy;
    this.inputEl.disabled = busy;
    this.sendBtn.disabled = busy;
    this.inputAreaEl.style.opacity = busy ? '0.5' : '1';
    if (!busy) this.inputEl.focus();
  }

  #addBubble(text, who) {
    const msg = document.createElement('div');
    msg.className = 'dd-msg ' + who;
    msg.textContent = text;
    this.logEl.appendChild(msg);
    requestAnimationFrame(() => this.reposition());
    return msg;
  }
}
