<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { http } from '../core/api/http';
import type { Notification } from '../core/api/types';
import { logout, session } from '../core/auth/session';
import { receiveAlert, receiveCamera, receiveNotification, liveNotification } from '../core/websocket/live';
import { useWebSocket } from '../core/websocket/useWebSocket';

const router = useRouter();
const notices = ref<Notification[]>([]);
const unread = ref(0);
const bellOpen = ref(false);
const menuOpen = ref(false);
const groups = [
  { title: 'GIÁM SÁT', items: [
    ['Giám sát camera', '/cameras', 'pi-video'], ['Sự kiện & cảnh báo', '/alerts', 'pi-exclamation-circle'],
    ['Hành động khắc phục', '/remediations', 'pi-check-square'], ['Giám sát quy trình', '', 'pi-sitemap'] ] },
  { title: 'BÁO CÁO & TRA CỨU', items: [
    ['Tổng quan an toàn', '/safety-overview', 'pi-chart-bar'], ['Báo cáo thống kê', '/statistics', 'pi-chart-line'],
    ['Lịch sử FaceID', '', 'pi-id-card'], ['Kết quả Pattern', '', 'pi-search'] ] },
  { title: 'CẤU HÌNH PHÂN TÍCH', items: [
    ['Kịch bản & Camera AI', '/scenarios', 'pi-sliders-h'], ['Danh mục hành vi', '/behaviors', 'pi-list'],
    ['Thiết kế quy trình', '', 'pi-share-alt'] ] },
  { title: 'THIẾT BỊ & VỊ TRÍ', items: [
    ['Camera', '/camera-management', 'pi-camera'], ['Địa điểm', '', 'pi-map-marker'], ['Thiết bị PLC', '', 'pi-bolt'] ] },
  { title: 'AI & NHẬN DIỆN', items: [
    ['Nền tảng phân tích AI', '', 'pi-sparkles'], ['Nhận dạng nhân viên', '', 'pi-users'], ['Mẫu Pattern', '', 'pi-images'] ] },
  { title: 'QUẢN TRỊ HỆ THỐNG', items: [
    ['Tình trạng hệ thống', '/system', 'pi-server'], ['Người dùng', '', 'pi-user'],
    ['Vai trò & Quyền hạn', '', 'pi-lock'], ['Nhật ký thao tác', '', 'pi-history'],
    ['Trung tâm hướng dẫn', '', 'pi-question-circle'] ] },
] as const;
let sockets: { stop: () => void }[] = [];
async function refreshNotices() {
  try {
    const { data } = await http.get('/notificationservice/notifications?pageSize=10');
    notices.value = data.items; unread.value = data.unreadCount;
  } catch { /* bell shows the last live items if service is unavailable */ }
}
async function openNotice(item: Notification) {
  if (!item.isRead) {
    await http.put(`/notificationservice/notifications/${item.id}/read`);
    item.isRead = true; unread.value = Math.max(0, unread.value - 1);
  }
  bellOpen.value = false;
  await router.push({ path: '/alerts', query: { id: item.alertId } });
}
function signOut() { sockets.forEach(item => item.stop()); logout(); router.push('/login'); }
watch(liveNotification, item => {
  if (item && !notices.value.some(old => old.id === item.id)) {
    notices.value.unshift(item); notices.value = notices.value.slice(0, 10); unread.value++;
  }
});
onMounted(() => {
  sockets = [useWebSocket('/ws/alerts', receiveAlert),
    useWebSocket('/ws/cameras', receiveCamera),
    useWebSocket('/ws/notifications', receiveNotification)];
  refreshNotices();
});
onUnmounted(() => sockets.forEach(item => item.stop()));
</script>

<template>
  <div class="app-shell">
    <aside class="sidebar" :class="{ shown: menuOpen }">
      <div class="sidebar-brand"><span class="brand-mark"><i class="pi pi-eye" /></span><span>LV-Vision<small>SAFETY INTELLIGENCE</small></span></div>
      <nav aria-label="Điều hướng chính">
        <section v-for="group in groups" :key="group.title" class="menu-group">
          <h2>{{ group.title }}</h2>
          <template v-for="item in group.items" :key="item[0]">
            <RouterLink v-if="item[1]" :to="item[1]" class="menu-item" @click="menuOpen = false"><i :class="`pi ${item[2]}`" />{{ item[0] }}</RouterLink>
            <span v-else class="menu-item disabled" :title="'Có trong roadmap giai đoạn sau'"><i :class="`pi ${item[2]}`" />{{ item[0] }}<small>SAU</small></span>
          </template>
        </section>
      </nav>
      <div class="sidebar-footer">LV-Vision · Giám sát an toàn</div>
    </aside>
    <div class="workspace">
      <header class="topbar">
        <button class="icon-button mobile-menu" aria-label="Mở menu" @click="menuOpen = !menuOpen"><i class="pi pi-bars" /></button>
        <div class="topbar-title"><span class="topbar-crumb">LV-VISION</span><strong>Trung tâm giám sát an toàn</strong></div>
        <div class="topbar-actions">
          <div class="bell-wrap">
            <button class="icon-button" aria-label="Thông báo" @click="bellOpen = !bellOpen"><i class="pi pi-bell" /><span v-if="unread" class="badge">{{ unread > 99 ? '99+' : unread }}</span></button>
            <div v-if="bellOpen" class="bell-panel">
              <div class="panel-heading"><strong>Thông báo</strong><span>{{ unread }} chưa đọc</span></div>
              <button v-for="item in notices" :key="item.id" class="notice-item" :class="{ unread: !item.isRead }" @click="openNotice(item)"><span class="severity-dot" :class="item.severity.toLowerCase()" /><span>{{ item.title }}<small>{{ new Date(item.createdAt).toLocaleString('vi-VN') }}</small></span></button>
              <p v-if="!notices.length" class="empty">Chưa có thông báo.</p>
            </div>
          </div>
          <div class="user-pill"><i class="pi pi-user" />{{ session.user?.userName || session.user?.userId || 'Người dùng' }}</div>
          <button class="icon-button" aria-label="Đăng xuất" title="Đăng xuất" @click="signOut"><i class="pi pi-sign-out" /></button>
        </div>
      </header>
      <main class="main-content"><RouterView /></main>
    </div>
  </div>
</template>
