import { createRouter, createWebHistory } from 'vue-router';
import { loadUser, session } from '../core/auth/session';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', component: () => import('../modules/auth/Login.vue'), meta: { public: true } },
    { path: '/', component: () => import('../layout/AppLayout.vue'), children: [
      { path: '', redirect: '/cameras' },
      { path: 'cameras', component: () => import('../modules/cameras/pages/CameraMonitor.vue'), meta: { permission: 'Camera.Read' } },
      { path: 'camera-management', component: () => import('../modules/cameras/pages/CameraManagement.vue'), meta: { permission: 'Camera.Read' } },
      { path: 'scenarios', component: () => import('../modules/cameras/pages/ScenarioManager.vue'), meta: { permission: 'Camera.Read' } },
      { path: 'alerts', component: () => import('../modules/behavior/pages/BehaviorEvents.vue'), meta: { permission: 'Alert.Read' } },
      { path: 'behaviors', component: () => import('../modules/behavior/pages/BehaviorTypes.vue'), meta: { permission: 'Behavior.Read' } },
      { path: 'remediations', component: () => import('../modules/remediation/pages/RemediationList.vue'), meta: { permission: 'Alert.Read' } },
      { path: 'safety-overview', component: () => import('../modules/reports/pages/SafetyOverview.vue'), meta: { permission: 'Alert.Read' } },
      { path: 'statistics', component: () => import('../modules/reports/pages/StatisticsReport.vue'), meta: { permission: 'Alert.Read' } },
      { path: 'system', component: () => import('../modules/system/pages/SystemHealth.vue'), meta: { permission: 'Camera.Read' } },
    ] },
    { path: '/:pathMatch(.*)*', redirect: '/cameras' },
  ],
});

router.beforeEach(async to => {
  if (to.meta.public) return session.token ? '/cameras' : true;
  if (!session.token) return '/login';
  if (!session.user && !await loadUser()) return '/login';
  const permission = to.meta.permission as string | undefined;
  if (permission && !session.user?.permissions.some(item => item === permission || item === permission.replace('.Read', '.Manage')))
    return '/cameras';
  return true;
});
export default router;
