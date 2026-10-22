import { ref } from 'vue';
import type { Alert, Camera, Notification } from '../api/types';

export const liveAlert = ref<Alert | null>(null);
export const liveCamera = ref<Camera | null>(null);
export const liveNotification = ref<Notification | null>(null);
export const liveAlertTick = ref(0);
export const liveCameraTick = ref(0);

export function receiveAlert(message: any): void {
  if (['NewAlert', 'AlertUpdate', 'ClipReady'].includes(message.type) && message.alert) {
    liveAlert.value = message.alert;
    liveAlertTick.value++;
  }
}
export function receiveCamera(message: any): void {
  if (message.type === 'CameraStatus' && message.camera) {
    liveCamera.value = message.camera;
    liveCameraTick.value++;
  }
}
export function receiveNotification(message: any): void {
  if (message.type === 'NewNotification' && message.notification) {
    liveNotification.value = message.notification;
  }
}
