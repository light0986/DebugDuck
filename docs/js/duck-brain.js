// DuckBrain.cs 的移植：小黃鴨除錯法，鴨子只會「嘎」。
// 台詞只有 5 種：嘎 / 嘎嘎 / 嘎嘎嘎 / ? / !

export const Reaction = Object.freeze({
  Idle: 'idle',
  Listen: 'listen',
  Talk: 'talk',
  Happy: 'happy',
  Drag: 'drag',
});

const QUACKS = ['嘎', '嘎嘎', '嘎嘎嘎'];

const SOLVED = ['解決', '搞定', '找到了', '原來', '懂了', '是這個',
  'fixed', 'solved', 'got it', 'works now', 'nailed it'];

const QUESTION = ['為什麼', '為何', '怎麼', '怎會', 'how', 'why', 'what'];

function containsAny(haystack, needles) {
  return needles.some((n) => haystack.includes(n));
}

export class DuckBrain {
  #lastQuack = '';

  /** @returns {{ text: string, reaction: string }} */
  respond(input) {
    const text = (input ?? '').trim();
    const lower = text.toLowerCase();

    // 你自己想通了 → ！
    if (containsAny(lower, SOLVED)) {
      return { text: '!', reaction: Reaction.Happy };
    }

    // 你在問問題 → ？（把問題丟回給你）
    if (text.length === 0 || text.includes('?') || text.includes('？') || containsAny(lower, QUESTION)) {
      return { text: '?', reaction: Reaction.Listen };
    }

    // 其他 → 嘎（繼續講）
    return { text: this.#nextQuack(), reaction: Reaction.Talk };
  }

  #nextQuack() {
    let q = QUACKS[Math.floor(Math.random() * QUACKS.length)];
    if (q === this.#lastQuack) {
      q = QUACKS[(QUACKS.indexOf(q) + 1) % QUACKS.length];
    }
    this.#lastQuack = q;
    return q;
  }
}
