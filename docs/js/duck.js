// PetWindow.xaml(.cs) 的移植：漂在頁面上的小鴨。
// 拖曳、左鍵點擊 Q 彈壓扁 + 叫聲、右鍵（或長按）選單、上下浮動、點頭、跳躍。

import { DuckSound } from './duck-sound.js';
import { Reaction } from './duck-brain.js';

const POS_KEY = 'debugduck.pos';
const DUCK_SIZE = 120;
const DRAG_THRESHOLD = 4;

function loadPos() {
  try {
    const p = JSON.parse(localStorage.getItem(POS_KEY) || 'null');
    if (p && Number.isFinite(p.left) && Number.isFinite(p.top)) return p;
  } catch { /* ignore */ }
  return null;
}
function savePos(left, top) {
  try { localStorage.setItem(POS_KEY, JSON.stringify({ left, top })); } catch { /* ignore */ }
}

export class Duck {
  /** @param {{ onTalk:Function, onHistory:Function, onAbout:Function }} handlers */
  constructor(handlers) {
    this.handlers = handlers;
    this.onMove = null;                 // 讓聊天氣泡跟著鴨子移動

    this._squashing = false;
    this._bobAnim = null;

    this.#buildDom();
    this.#restorePosition();
    this.#startBob();
    this.#wireDrag();
    this.#wireMenu();

    window.addEventListener('resize', () => this.#clampIntoView());
    // 版面 / 尺寸底定後再夾一次
    requestAnimationFrame(() => this.#clampIntoView());
    window.addEventListener('load', () => this.#clampIntoView());
  }

  // ---------- DOM ----------

  #buildDom() {
    const el = document.createElement('div');
    el.className = 'dd-duck';
    el.style.width = DUCK_SIZE + 'px';
    el.innerHTML =
      '<div class="dd-translate"><div class="dd-rotate"><div class="dd-scale">' +
      '<img src="assets/duck.png" alt="小黃鴨" draggable="false"></div></div></div>';
    document.body.appendChild(el);
    this.el = el;
    this.translateEl = el.querySelector('.dd-translate');
    this.rotateEl = el.querySelector('.dd-rotate');
    this.scaleEl = el.querySelector('.dd-scale');

    const menu = document.createElement('div');
    menu.className = 'dd-menu';
    menu.hidden = true;
    menu.innerHTML =
      '<button data-action="talk">跟鴨子說話</button>' +
      '<button data-action="history">查看歷史訊息</button>' +
      '<button data-action="center">回到畫面中央</button>' +
      '<button data-action="about">關於</button>';
    document.body.appendChild(menu);
    this.menuEl = menu;
  }

  // ---------- 位置 ----------
  //
  // 預設靠右下角是靠 CSS 的 right / bottom（不依賴 JS 量到的視窗尺寸，比較穩）。
  // 一旦拖曳過、或有存過位置，就改用 left / top 絕對定位。

  rect() { return this.el.getBoundingClientRect(); }

  #setPos(left, top) {
    const s = this.el.style;
    s.left = left + 'px';
    s.top = top + 'px';
    s.right = 'auto';
    s.bottom = 'auto';
    if (this.onMove) this.onMove();
  }

  #usingLeftTop() {
    return this.el.style.left && this.el.style.left !== 'auto';
  }

  #restorePosition() {
    const p = loadPos();
    if (p) this.#setPos(p.left, p.top);   // 沒有存過就維持 CSS 的右下角
  }

  #clampIntoView() {
    if (!this.#usingLeftTop()) return;                       // CSS right/bottom 模式不用夾
    if (!window.innerWidth || !window.innerHeight) return;   // 視窗還沒有尺寸
    const r = this.rect();
    if (r.width === 0 || r.height === 0) return;
    const left = Math.min(Math.max(r.left, 0), Math.max(0, window.innerWidth - r.width));
    const top = Math.min(Math.max(r.top, 0), Math.max(0, window.innerHeight - r.height));
    if (left !== r.left || top !== r.top) this.#setPos(left, top);
  }

  centerToDefault() {
    const s = this.el.style;
    s.left = 'auto';
    s.top = 'auto';
    s.right = '24px';
    s.bottom = '16px';
    try { localStorage.removeItem(POS_KEY); } catch { /* ignore */ }
    if (this.onMove) this.onMove();
  }

  // ---------- 拖曳 / 點擊 ----------

  #wireDrag() {
    let pressed = false, dragging = false;
    let startX = 0, startY = 0, originLeft = 0, originTop = 0;
    let longPressTimer = null, longPressed = false;

    const clearLongPress = () => {
      if (longPressTimer) { clearTimeout(longPressTimer); longPressTimer = null; }
    };

    this.el.addEventListener('pointerdown', (e) => {
      if (e.button === 2) return;             // 右鍵交給 contextmenu
      pressed = true;
      dragging = false;
      longPressed = false;
      const r = this.rect();
      startX = e.clientX;
      startY = e.clientY;
      originLeft = r.left;
      originTop = r.top;
      this.el.setPointerCapture(e.pointerId);

      if (e.pointerType === 'touch') {
        longPressTimer = setTimeout(() => {
          longPressed = true;
          this.#openMenu(startX, startY);
        }, 500);
      }
    });

    this.el.addEventListener('pointermove', (e) => {
      if (!pressed) return;
      const dx = e.clientX - startX;
      const dy = e.clientY - startY;

      if (!dragging && Math.abs(dx) + Math.abs(dy) > DRAG_THRESHOLD) {
        dragging = true;
        clearLongPress();
        if (!longPressed) this.nod();
      }
      if (dragging) {
        this.#setPos(originLeft + dx, originTop + dy);
      }
    });

    const end = () => {
      if (!pressed) return;
      pressed = false;
      clearLongPress();

      if (dragging) {
        const r = this.rect();
        savePos(r.left, r.top);
      } else if (!longPressed) {
        this.squash();                       // 純點擊 → 壓扁 + 叫一聲
      }
      dragging = false;
    };
    this.el.addEventListener('pointerup', end);
    this.el.addEventListener('pointercancel', end);
  }

  // ---------- 右鍵選單 ----------

  #wireMenu() {
    this.el.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      this.#openMenu(e.clientX, e.clientY);
    });

    this.menuEl.addEventListener('click', (e) => {
      const btn = e.target.closest('button');
      if (!btn) return;
      this.#closeMenu();
      switch (btn.dataset.action) {
        case 'talk': this.handlers.onTalk?.(); break;
        case 'history': this.handlers.onHistory?.(); break;
        case 'center': this.centerToDefault(); break;
        case 'about': this.handlers.onAbout?.(); break;
      }
    });

    document.addEventListener('pointerdown', (e) => {
      if (!this.menuEl.hidden && !this.menuEl.contains(e.target)) this.#closeMenu();
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') this.#closeMenu();
    });
    window.addEventListener('scroll', () => this.#closeMenu(), true);
  }

  #openMenu(x, y) {
    const m = this.menuEl;
    m.hidden = false;
    // 出現後量尺寸再夾進畫面
    const w = m.offsetWidth, h = m.offsetHeight;
    const left = Math.min(x, window.innerWidth - w - 6);
    const top = Math.min(y, window.innerHeight - h - 6);
    m.style.left = Math.max(6, left) + 'px';
    m.style.top = Math.max(6, top) + 'px';
  }

  #closeMenu() {
    this.menuEl.hidden = true;
  }

  // ---------- 動畫 ----------

  #startBob() {
    this._bobAnim?.cancel();
    this._bobAnim = this.translateEl.animate(
      [{ transform: 'translateY(0)' }, { transform: 'translateY(-3px)' }, { transform: 'translateY(0)' }],
      { duration: 2400, iterations: Infinity, easing: 'ease-in-out' },
    );
  }

  nod() {
    this.rotateEl.animate(
      [
        { transform: 'rotate(0deg)', offset: 0 },
        { transform: 'rotate(-9deg)', offset: 0.28 },
        { transform: 'rotate(4deg)', offset: 0.64 },
        { transform: 'rotate(0deg)', offset: 1 },
      ],
      { duration: 500, easing: 'ease-in-out' },
    );
  }

  hop() {
    this._bobAnim?.cancel();
    const a = this.translateEl.animate(
      [
        { transform: 'translateY(0)', offset: 0 },
        { transform: 'translateY(-16px)', offset: 0.25, easing: 'ease-out' },
        { transform: 'translateY(0)', offset: 0.55 },
        { transform: 'translateY(-7px)', offset: 0.76 },
        { transform: 'translateY(0)', offset: 1 },
      ],
      { duration: 720, easing: 'ease-in-out' },
    );
    a.addEventListener('finish', () => this.#startBob());
    a.addEventListener('cancel', () => this.#startBob());
  }

  squash() {
    if (this._squashing) return;
    this._squashing = true;

    DuckSound.play(1);   // 點擊變形時叫一聲

    const a = this.scaleEl.animate(
      [
        { transform: 'scale(1, 1)', offset: 0 },
        { transform: 'scale(1.08, 0.90)', offset: 0.24, easing: 'ease-out' },
        { transform: 'scale(0.98, 1.03)', offset: 0.55 },
        { transform: 'scale(1.01, 0.99)', offset: 0.78 },
        { transform: 'scale(1, 1)', offset: 1 },
      ],
      { duration: 500, easing: 'ease-in-out' },
    );
    const done = () => { this._squashing = false; };
    a.addEventListener('finish', done);
    a.addEventListener('cancel', done);
  }

  /** 聊天氣泡依鴨子的回應叫這個。 */
  react(reaction) {
    if (reaction === Reaction.Happy) this.hop();
    else if (reaction === Reaction.Talk || reaction === Reaction.Listen) this.nod();
  }
}
