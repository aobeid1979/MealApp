type AuthSession = {
  token: string;
  email: string;
  name: string;
  role: string;
};

type JwtPayload = {
  exp?: number;
};

const authKeys = ["token", "email", "name", "role"];
const redirectPathKey = "redirectPath";

function isBrowser() {
  return typeof window !== "undefined";
}

export function saveAuthSession(session: AuthSession) {
  if (!isBrowser()) {
    return;
  }

  clearLegacyLocalStorage();

  sessionStorage.setItem("token", session.token);
  sessionStorage.setItem("email", session.email);
  sessionStorage.setItem("name", session.name);
  sessionStorage.setItem("role", session.role);
}

export function clearAuthSession() {
  if (!isBrowser()) {
    return;
  }

  authKeys.forEach((key) => {
    sessionStorage.removeItem(key);
    localStorage.removeItem(key);
  });
}

export function getValidToken() {
  if (!isBrowser()) {
    return null;
  }

  const token = sessionStorage.getItem("token");

  if (!token || isJwtExpired(token)) {
    clearAuthSession();
    return null;
  }

  return token;
}

export function getTokenTimeRemaining(token: string) {
  const payload = decodeJwtPayload(token);

  if (!payload?.exp) {
    return 0;
  }

  return Math.max(payload.exp * 1000 - Date.now(), 0);
}

export function getRole() {
  if (!getValidToken()) {
    return null;
  }

  return sessionStorage.getItem("role");
}

export function getName() {
  if (!getValidToken()) {
    return null;
  }

  return sessionStorage.getItem("name");
}

export function getEmail() {
  if (!getValidToken()) {
    return null;
  }

  return sessionStorage.getItem("email");
}

export function setRedirectPath(path: string) {
  if (!isBrowser()) {
    return;
  }

  sessionStorage.setItem(redirectPathKey, path);
}

export function getRedirectPath() {
  if (!isBrowser()) {
    return null;
  }

  return sessionStorage.getItem(redirectPathKey);
}

export function clearRedirectPath() {
  if (!isBrowser()) {
    return;
  }

  sessionStorage.removeItem(redirectPathKey);
  localStorage.removeItem(redirectPathKey);
}

function clearLegacyLocalStorage() {
  authKeys.forEach((key) => localStorage.removeItem(key));
}

function isJwtExpired(token: string) {
  const payload = decodeJwtPayload(token);

  if (!payload?.exp) {
    return true;
  }

  return payload.exp * 1000 <= Date.now();
}

function decodeJwtPayload(token: string): JwtPayload | null {
  const parts = token.split(".");

  if (parts.length !== 3) {
    return null;
  }

  try {
    const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const json = atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, "="));

    return JSON.parse(json) as JwtPayload;
  } catch {
    return null;
  }
}
