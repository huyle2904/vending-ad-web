import { Card } from "../components/ui/Card";

export function SettingsPage() {
  return (
    <div className="p-6 space-y-6">
      <h2>Settings</h2>

      <Card>
        <div className="text-center py-12">
          <p className="text-muted-foreground">Cập nhật sau</p>
        </div>
      </Card>
    </div>
  );
}
