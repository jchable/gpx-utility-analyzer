import Spinner from './Spinner';

interface LoadingStateProps {
  className?: string;
}

export function LoadingState({ className = 'h-96' }: LoadingStateProps) {
  return (
    <div className={`flex items-center justify-center ${className}`}>
      <Spinner size="lg" />
    </div>
  );
}

interface ErrorStateProps {
  message: string;
  className?: string;
}

export function ErrorState({ message, className = 'h-96' }: ErrorStateProps) {
  return (
    <div className={`flex items-center justify-center ${className}`}>
      <p className="text-accent-red text-lg">{message}</p>
    </div>
  );
}
