import { defineConfig, devices } from '@playwright/test'
import { backendUrl, frontendUrl } from './e2e/support/environment.ts'

export default defineConfig({
  testDir: './e2e',
  outputDir: './test-results',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  ...(process.env.CI ? { workers: 2 } : {}),
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
  ],
  use: {
    baseURL: frontendUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'mobile-webkit',
      testMatch: [
        'mobile-layout.spec.ts',
        'lobby-creation.spec.ts',
        'lobby-joining.spec.ts',
        'party-lifecycle.spec.ts',
      ],
      use: { ...devices['iPhone 13'] },
    },
    {
      name: 'chromium',
      testIgnore: 'online-matchmaking.spec.ts',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'mobile-chromium',
      testIgnore: 'online-matchmaking.spec.ts',
      use: { ...devices['Pixel 5'] },
    },
    // One shared FIFO queue: this journey runs both viewports sequentially to avoid matching parallel test players.
    {
      name: 'online-chromium',
      testMatch: 'online-matchmaking.spec.ts',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'online-webkit',
      testMatch: 'online-matchmaking.spec.ts',
      dependencies: ['online-chromium'],
      use: { ...devices['iPhone 13'] },
    },
  ],
  webServer: [
    {
      command: 'npm run dev -- --host 127.0.0.1 --port 5175 --strictPort',
      url: frontendUrl,
      env: { VITE_API_BASE_URL: backendUrl },
      reuseExistingServer: false,
      timeout: 60_000,
    },
    {
      command:
        'dotnet run --project ../backend/WordGame.Api --configuration Release --no-launch-profile --urls http://127.0.0.1:5085',
      url: backendUrl + '/health',
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        // Parallel browser identities share one loopback IP; security tests exercise low limits separately.
        LobbyRateLimits__CreatePermitsPerMinute: '1000',
        LobbyRateLimits__JoinPermitsPerMinute: '1000',
        LobbyRateLimits__GuestPermitsPerMinute: '1000',
        LobbyRateLimits__MatchmakingPermitsPerMinute: '1000',
        Cors__AllowedOrigins__0: frontendUrl,
        Cors__AllowedOrigins__1: 'http://localhost:5175',
      },
      reuseExistingServer: false,
      timeout: 60_000,
    },
  ],
})
