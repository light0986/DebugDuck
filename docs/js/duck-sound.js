// DuckSound.cs 的移植。
// 桌面版是「一組 MediaPlayer 輪流播、聲音疊在一起」；
// 網頁用 Web Audio API 更乾脆：每一聲 = 一個新的 BufferSource，天生就能重疊。

const SRC = 'assets/duck.mp3';

export const DuckSound = {
  enabled: true,

  /** 連叫時每一聲開始的間隔（毫秒）。比音檔（~910ms）短就會互相重疊＝混音效果。 */
  quackStagger: 250,

  /** 每一聲的音量（多聲重疊時總和會變大，壓低一點）。 */
  volume: 0.5,

  /** @type {AudioContext|null} */
  _ctx: null,
  /** @type {AudioBuffer|null} */
  _buf: null,
  _loading: null,

  /** 在第一個使用者操作時呼叫一次，把 AudioContext 建好、把音檔載進來。 */
  init() {
    if (this._ctx) return;
    try {
      const Ctx = window.AudioContext || window.webkitAudioContext;
      this._ctx = new Ctx();
      this._loading = fetch(SRC)
        .then((r) => r.arrayBuffer())
        .then((ab) => this._ctx.decodeAudioData(ab))
        .then((buf) => { this._buf = buf; })
        .catch(() => { /* 載不到就靜音，不要壞掉 */ });
    } catch {
      this._ctx = null;
    }
  },

  /** 依台詞字串發出對應聲數：嘎嘎→2、嘎嘎嘎→3、其餘（嘎 / ? / !）→1。 */
  speak(line) {
    this.play(line === '嘎嘎' ? 2 : line === '嘎嘎嘎' ? 3 : 1);
  },

  /** 連叫 times 聲，每聲相隔 quackStagger 毫秒，聲音會疊在一起。 */
  play(times = 1) {
    if (!this.enabled || times < 1) return;
    this.init();
    if (!this._ctx) return;
    if (this._ctx.state === 'suspended') this._ctx.resume();

    const fire = () => {
      if (!this._buf) return;
      const ctx = this._ctx;
      const src = ctx.createBufferSource();
      src.buffer = this._buf;
      const gain = ctx.createGain();
      gain.gain.value = this.volume;
      src.connect(gain).connect(ctx.destination);
      src.start();
    };

    const run = () => {
      fire();
      for (let i = 1; i < times; i++) {
        setTimeout(fire, i * this.quackStagger);
      }
    };

    // 音檔還沒解碼完就等它
    if (this._buf) run();
    else if (this._loading) this._loading.then(run);
  },
};
