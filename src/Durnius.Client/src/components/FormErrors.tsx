interface FormErrorsProps {
  errors: string[];
}

export default function FormErrors({ errors }: FormErrorsProps) {
  if (errors.length === 0) {
    return null;
  }

  return (
    <div className="error" role="alert">
      {errors.length === 1 ? (
        errors[0]
      ) : (
        <ul>
          {errors.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
