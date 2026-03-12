import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../contexts/AuthContext';

export default function RegisterPage() {
  const { t } = useTranslation('auth');
  const { register } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      await register(email, password, displayName);
      navigate('/', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? t(`error.${err.message}`, err.message) : t('error.REGISTER_FAILED'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-surface px-4">
      <div className="w-full max-w-md space-y-8">
        <div className="text-center">
          <h1 className="text-3xl font-bold text-accent">GPX Analyzer</h1>
          <p className="mt-2 text-content-muted">{t('registerSubtitle')}</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-6 bg-surface-alt p-8 rounded-xl border border-border">
          <h2 className="text-xl font-semibold text-content">{t('register')}</h2>

          {error && (
            <div className="bg-red-500/10 text-red-400 text-sm px-4 py-3 rounded-lg border border-red-500/20">
              {error}
            </div>
          )}

          <div>
            <label htmlFor="displayName" className="block text-sm font-medium text-content-muted mb-1">
              {t('displayName')}
            </label>
            <input
              id="displayName"
              type="text"
              required
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              className="w-full px-4 py-2.5 rounded-lg bg-surface border border-border text-content focus:outline-none focus:ring-2 focus:ring-accent/50"
              placeholder={t('displayNamePlaceholder')}
            />
          </div>

          <div>
            <label htmlFor="email" className="block text-sm font-medium text-content-muted mb-1">
              {t('email')}
            </label>
            <input
              id="email"
              type="email"
              required
              value={email}
              onChange={e => setEmail(e.target.value)}
              className="w-full px-4 py-2.5 rounded-lg bg-surface border border-border text-content focus:outline-none focus:ring-2 focus:ring-accent/50"
              placeholder={t('emailPlaceholder')}
            />
          </div>

          <div>
            <label htmlFor="password" className="block text-sm font-medium text-content-muted mb-1">
              {t('password')}
            </label>
            <input
              id="password"
              type="password"
              required
              minLength={8}
              value={password}
              onChange={e => setPassword(e.target.value)}
              className="w-full px-4 py-2.5 rounded-lg bg-surface border border-border text-content focus:outline-none focus:ring-2 focus:ring-accent/50"
              placeholder={t('passwordPlaceholder')}
            />
            <p className="text-xs text-content-muted mt-1">{t('passwordHint')}</p>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full py-2.5 px-4 bg-accent text-white rounded-lg font-medium hover:bg-accent/90 disabled:opacity-50 transition-colors"
          >
            {loading ? t('registering') : t('register')}
          </button>

          <p className="text-center text-sm text-content-muted">
            {t('hasAccount')}{' '}
            <Link to="/login" className="text-accent hover:underline">
              {t('loginLink')}
            </Link>
          </p>
        </form>
      </div>
    </div>
  );
}
