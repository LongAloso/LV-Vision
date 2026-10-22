import { reactive } from 'vue';
import { http } from '../api/http';

export interface User { userId: string; userName?: string; permissions: string[]; isActive: boolean }
const tokenKey = 'lv-vision-token';
export const session = reactive<{ token: string; user: User | null }>({
  token: sessionStorage.getItem(tokenKey) || '', user: null,
});

export function can(permission: string): boolean {
  return !!session.user?.permissions.includes(permission);
}

export async function login(name: string, password: string): Promise<void> {
  const { data } = await http.post('/identityservice/api/auth/login',
    { userNameOrEmail: name, password });
  session.token = data.accessToken || data.token;
  sessionStorage.setItem(tokenKey, session.token);
  await loadUser();
}

export async function loadUser(): Promise<User | null> {
  if (!session.token) return null;
  try {
    const { data } = await http.get<User>('/identityservice/api/auth/me');
    session.user = data;
    return data;
  } catch {
    logout();
    return null;
  }
}

export function logout(): void {
  session.token = '';
  session.user = null;
  sessionStorage.removeItem(tokenKey);
}
