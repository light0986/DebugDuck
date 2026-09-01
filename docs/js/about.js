// AboutWindow.xaml(.cs) 的移植：左上圓形頭像 + 作者 + 圓角亮灰「確認」。

export class About {
  constructor() {
    const back = document.createElement('div');
    back.className = 'dd-modal-back';
    back.hidden = true;
    back.innerHTML =
      '<div class="dd-modal dd-about">' +
      '  <img class="dd-about-avatar" src="assets/light0986.png" alt="">' +
      '  <div class="dd-about-author">作者: light0986</div>' +
      '  <button type="button" class="dd-pill dd-pill-grey" data-act="ok">確認</button>' +
      '</div>';
    document.body.appendChild(back);
    this.backEl = back;

    back.addEventListener('click', (e) => {
      if (e.target === back || e.target.closest('button')?.dataset.act === 'ok') this.close();
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && !back.hidden) this.close();
    });
  }

  open() { this.backEl.hidden = false; }
  close() { this.backEl.hidden = true; }
}
