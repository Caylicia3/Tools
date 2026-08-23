const CDN_BASE = 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.9';
const MODEL_URL = 'https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task';

export function initGestureEngine({ video, sensitivity = 0.5, onGesture }) {
  let landmarker = null;
  let running = false;
  let rafId = null;
  let lastGestureTime = 0;
  let lastTipY = null;
  let initialized = false;
  const debounceMs = 500;

  const TIP_INDICES = [8, 12, 16, 20];

  function dist(a, b) {
    return Math.hypot(a.x - b.x, a.y - b.y, (a.z || 0) - (b.z || 0));
  }

  function classify(landmarks) {
    const wrist = landmarks[0];
    const middleMcp = landmarks[9];
    const palmSize = dist(wrist, middleMcp) || 0.001;

    const avgTipDist = TIP_INDICES
      .map((i) => landmarks[i])
      .reduce((sum, tip) => sum + dist(wrist, tip), 0) / TIP_INDICES.length;

    const ratio = avgTipDist / palmSize;
    const fistThreshold = 0.9 - sensitivity * 0.15;
    const openThreshold = 1.1 + sensitivity * 0.15;

    let pose = 'none';
    if (ratio < fistThreshold) {
      pose = 'fist';
    } else if (ratio > openThreshold) {
      pose = 'open';
    }

    const indexTip = landmarks[8];
    let scroll = null;
    if (lastTipY != null) {
      const dy = lastTipY - indexTip.y;
      const threshold = 0.06 - sensitivity * 0.04;
      if (Math.abs(dy) > threshold) {
        scroll = dy > 0 ? 'scroll-up' : 'scroll-down';
      }
    }
    lastTipY = indexTip.y;

    return { pose, scroll };
  }

  function emit(gesture) {
    const now = Date.now();
    if (now - lastGestureTime < debounceMs) return;
    lastGestureTime = now;
    if (typeof onGesture === 'function') {
      onGesture(gesture);
    }
  }

  async function init() {
    if (initialized) return;
    const module = await import(`${CDN_BASE}/+esm`);
    const { FilesetResolver, HandLandmarker } = module;
    const vision = await FilesetResolver.forVisionTasks(`${CDN_BASE}/wasm/`);
    landmarker = await HandLandmarker.createFromOptions(vision, {
      baseOptions: {
        modelAssetPath: MODEL_URL,
        delegate: 'GPU'
      },
      runningMode: 'VIDEO',
      numHands: 1
    });
    initialized = true;
  }

  function loop() {
    if (!running) return;
    if (video.readyState >= 2 && landmarker) {
      const result = landmarker.detectForVideo(video, performance.now());
      if (result && result.landmarks && result.landmarks.length > 0) {
        const { pose, scroll } = classify(result.landmarks[0]);
        if (scroll) {
          emit(scroll);
        } else if (pose === 'fist') {
          emit('select');
        } else if (pose === 'open') {
          emit('open');
        }
      } else {
        lastTipY = null;
      }
    }
    rafId = requestAnimationFrame(loop);
  }

  async function start() {
    if (running) return;
    try {
      await init();
      running = true;
      loop();
    } catch (error) {
      console.error('[GestureEngine] init failed', error);
      if (typeof onGesture === 'function') onGesture('error');
    }
  }

  function stop() {
    running = false;
    if (rafId) {
      cancelAnimationFrame(rafId);
      rafId = null;
    }
  }

  function destroy() {
    stop();
    if (landmarker && typeof landmarker.close === 'function') {
      landmarker.close();
    }
    landmarker = null;
  }

  return {
    start,
    stop,
    destroy
  };
}
