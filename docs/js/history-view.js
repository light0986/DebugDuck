// HistoryWindow.xaml(.cs) 的移植：完整聊天記錄（只讀），以時間戳記斷句。

import { ChatHistory } from './chat-history.js';

function pad(n) { return String(n).padStart(2, '0'); }

function localStamp(iso) {
  const d = new Date(iso);
  return `${d.getFullYear()}/${pad(d.getMonth() + 1)}/${pad(d.getDate())}  ` +
    `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}

export class HistoryView {
  constructor() {
    this.#buildDom();
    this.#wire();
  }

  #buildDom() {
    const back = document.createElement('div');
    back.className = 'dd-modal-back';
    back.hidden = true;
    back.innerHTML =
      '<div class="dd-modal dd-history">' +
      '  <div class="dd-history-head">' +
      '    <span class="dd-history-title">完整聊天記錄</span>' +
      '    <span class="dd-history-actions">' +
      '      <button type="button" class="dd-pill dd-pill-blue" data-act="refresh">重新整理</button>' +
      '      <button type="button" class="dd-pill dd-pill-blue" data-act="close">關閉</button>' +
      '    </span>' +
      '  </div>' +
      '  <div class="dd-history-list"></div>' +
      '</div>';
    document.body.appendChild(back);

    this.backEl = back;
    this.titleEl = back.querySelector('.dd-history-title');
    this.listEl = back.querySelector('.dd-history-list');
  }

  #wire() {
    this.backEl.addEventListener('click', (e) => {
      const btn = e.target.closest('button');
      if (btn?.dataset.act === 'refresh') { this.#render(); return; }
      if (btn?.dataset.act === 'close' || e.target === this.backEl) this.close();
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && !this.backEl.hidden) this.close();
    });
  }

  open() {
    this.#render();
    this.backEl.hidden = false;
  }

  close() {
    this.backEl.hidden = true;
  }

  #render() {
    const entries = ChatHistory.entries();
    this.titleEl.textContent = `完整聊天記錄 · 共 ${entries.length} 則`;
    this.listEl.replaceChildren();

    if (entries.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'dd-history-empty';
      empty.textContent = '還沒有任何訊息。';
      this.listEl.appendChild(empty);
      return;
    }

    for (const entry of entries) {
      const block = document.createElement('div');
      block.className = 'dd-history-block';

      const stamp = document.createElement('div');
      stamp.className = 'dd-history-stamp';
      stamp.textContent = localStamp(entry.t);

      const msg = document.createElement('div');
      msg.className = 'dd-msg player';
      msg.textContent = entry.text;

      block.append(stamp, msg);
      this.listEl.appendChild(block);
    }
    this.listEl.scrollTop = this.listEl.scrollHeight;
  }
}
