import { AlertCircle } from "lucide-react";
import type { ReactNode } from "react";

function cn(...classes: (string | false | null | undefined)[]) {
  return classes.filter(Boolean).join(" ");
}
interface AlertProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: "default" | "destructive";
  children: ReactNode;
}

export const Alert = ({ variant = "default", children, className, ...props }: AlertProps) => {
  return (
    <div
      className={cn(
        "border p-4 rounded-md relative",
        variant === "destructive" ? "bg-red-100 border-red-400 text-red-800" : "bg-gray-100 border-gray-300 text-gray-800",
        className
      )}
      {...props}
    >
      {variant === "destructive" && (
        <AlertCircle className="w-5 h-5 inline-block mr-2" />
      )}
      {children}
    </div>
  );
};

export const AlertTitle = ({ children }: { children: ReactNode }) => (
  <h5 className="font-semibold mb-1">{children}</h5>
);

export const AlertDescription = ({ children }: { children: ReactNode }) => (
  <p className="text-sm">{children}</p>
);
