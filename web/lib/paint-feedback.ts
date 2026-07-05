/** localStorage key — set to "1" to mute paint SFX / haptics. */
export const PAINT_FEEDBACK_DISABLED_KEY = "citymajor_paint_feedback_off";

export type PaintFeedbackKind = "zone" | "bulldoze" | "road";

/** Dispatched on each bulldoze paint — ZoningToolbar listens for confirm flash. */
export const BULLDOZE_CONFIRM_EVENT = "citymajor:bulldoze-confirm";

export function notifyBulldozeConfirm(): void {
  if (typeof window === "undefined") return;
  window.dispatchEvent(new CustomEvent(BULLDOZE_CONFIRM_EVENT));
}

let audioCtx: AudioContext | null = null;

function isFeedbackDisabled(): boolean {
  if (typeof window === "undefined") return true;
  try {
    return window.localStorage.getItem(PAINT_FEEDBACK_DISABLED_KEY) === "1";
  } catch {
    return false;
  }
}

function getAudioContext(): AudioContext | null {
  if (typeof window === "undefined") return null;
  try {
    if (!audioCtx) audioCtx = new AudioContext();
    return audioCtx;
  } catch {
    return null;
  }
}

function prefersReducedMotion(): boolean {
  if (typeof window === "undefined") return true;
  try {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  } catch {
    return false;
  }
}

/** Light bulldoze paint haptic (ms) — skipped when reduced-motion is preferred. */
const BULLDOZE_HAPTIC_MS = 12;

function playBulldozeHaptic(): void {
  if (prefersReducedMotion()) return;
  try {
    navigator.vibrate?.(BULLDOZE_HAPTIC_MS);
  } catch {
    // Haptics unsupported — skip.
  }
}

const TONE_HZ: Record<PaintFeedbackKind, number> = {
  zone: 520,
  bulldoze: 200,
  road: 340,
};

/** Peak gain — scaled by pitch/waveform so rapid zone/road/bulldoze drags feel equal. */
const TONE_GAIN: Record<PaintFeedbackKind, number> = {
  zone: 0.036,
  bulldoze: 0.048,
  road: 0.042,
};

const TONE_DECAY_S: Record<PaintFeedbackKind, number> = {
  zone: 0.09,
  bulldoze: 0.11,
  road: 0.09,
};

/**
 * Short paint blip + light haptic. Silently no-ops when audio/haptics are
 * unavailable, autoplay-blocked, or the user disabled feedback.
 */
export function playPaintFeedback(kind: PaintFeedbackKind): void {
  if (kind === "bulldoze") notifyBulldozeConfirm();

  if (isFeedbackDisabled()) return;

  const ctx = getAudioContext();
  if (ctx) {
    try {
      if (ctx.state === "suspended") void ctx.resume();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      const decayS = TONE_DECAY_S[kind];
      osc.type = kind === "bulldoze" ? "triangle" : "sine";
      osc.frequency.value = TONE_HZ[kind];
      gain.gain.setValueAtTime(TONE_GAIN[kind], ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + decayS);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(ctx.currentTime);
      osc.stop(ctx.currentTime + decayS + 0.01);
    } catch {
      // Autoplay policy or missing Web Audio — skip sound.
    }
  }

  if (kind === "bulldoze") playBulldozeHaptic();
}

export function setPaintFeedbackEnabled(enabled: boolean): void {
  if (typeof window === "undefined") return;
  try {
    if (enabled) {
      window.localStorage.removeItem(PAINT_FEEDBACK_DISABLED_KEY);
    } else {
      window.localStorage.setItem(PAINT_FEEDBACK_DISABLED_KEY, "1");
    }
  } catch {
    // Private browsing — ignore.
  }
}

export function isPaintFeedbackEnabled(): boolean {
  return !isFeedbackDisabled();
}
