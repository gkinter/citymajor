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

const TONE_HZ: Record<PaintFeedbackKind, number> = {
  zone: 520,
  bulldoze: 200,
  road: 340,
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
      osc.type = "sine";
      osc.frequency.value = TONE_HZ[kind];
      gain.gain.setValueAtTime(0.07, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.09);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(ctx.currentTime);
      osc.stop(ctx.currentTime + 0.1);
    } catch {
      // Autoplay policy or missing Web Audio — skip sound.
    }
  }

  try {
    navigator.vibrate?.(kind === "bulldoze" ? 12 : 6);
  } catch {
    // Haptics unsupported — skip.
  }
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
