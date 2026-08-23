export function initCamera({ videoElement, onError }) {
  let stream = null;
  let active = false;

  async function start() {
    if (active) return;
    try {
      stream = await navigator.mediaDevices.getUserMedia({
        video: {
          width: { ideal: 320 },
          height: { ideal: 240 },
          facingMode: 'user'
        },
        audio: false
      });
      videoElement.srcObject = stream;
      active = true;
    } catch (error) {
      active = false;
      if (onError) onError(error);
      throw error;
    }
  }

  function stop() {
    if (stream) {
      stream.getTracks().forEach((track) => track.stop());
      stream = null;
    }
    videoElement.pause();
    videoElement.srcObject = null;
    active = false;
  }

  return {
    start,
    stop,
    isActive() {
      return active;
    }
  };
}
