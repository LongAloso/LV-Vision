<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { errorText } from '../../core/api/http';
import { login } from '../../core/auth/session';

const router = useRouter();
const name = ref('');
const password = ref('');
const busy = ref(false);
const error = ref('');
async function submit() {
  busy.value = true; error.value = '';
  try { await login(name.value, password.value); await router.push('/cameras'); }
  catch (reason) { error.value = errorText(reason); }
  finally { busy.value = false; }
}
</script>

<template>
  <main class="login-screen">
    <form class="login-card" @submit.prevent="submit">
      <div class="brand-symbol"><i class="pi pi-eye" /></div>
      <div class="eyebrow">NỀN TẢNG GIÁM SÁT AN TOÀN</div>
      <h1>LV-Vision</h1>
      <p>Đăng nhập để xem camera, cảnh báo và tình trạng hệ thống.</p>
      <label>Tên đăng nhập hoặc email<input v-model="name" autocomplete="username" required /></label>
      <label>Mật khẩu<input v-model="password" type="password" autocomplete="current-password" required /></label>
      <div v-if="error" class="error-box" role="alert">{{ error }}</div>
      <button class="primary-button" :disabled="busy">{{ busy ? 'Đang đăng nhập...' : 'Đăng nhập' }}</button>
    </form>
  </main>
</template>
