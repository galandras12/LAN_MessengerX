import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import LanMessenger 1.0

Page {
    id: page
    title: qsTr("Message History")

    Component.onCompleted: messenger.refreshHistory()

    header: ToolBar {
        Material.foreground: "white"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 4
            ToolButton {
                text: "←"
                onClicked: StackView.view.pop()
            }
            Label {
                text: page.title
                font.pixelSize: 18
                Layout.fillWidth: true
            }
            ToolButton {
                text: qsTr("Clear")
                enabled: historyList.count > 0
                onClicked: clearConfirmDialog.open()
            }
        }
    }

    Dialog {
        id: clearConfirmDialog
        anchors.centerIn: parent
        modal: true
        title: qsTr("Clear history?")
        standardButtons: Dialog.Yes | Dialog.No
        onAccepted: messenger.clearHistory()

        Label {
            text: qsTr("This permanently deletes every saved conversation entry. This cannot be undone.")
            wrapMode: Text.Wrap
        }
    }

    ListView {
        id: historyList
        anchors.fill: parent
        clip: true
        model: messenger.history

        delegate: ItemDelegate {
            width: ListView.view.width
            height: 56

            contentItem: ColumnLayout {
                spacing: 2
                Text {
                    text: name
                    font.pixelSize: 15
                    elide: Text.ElideRight
                    Layout.fillWidth: true
                }
                Text {
                    text: date
                    font.pixelSize: 11
                    opacity: 0.6
                }
            }

            onClicked: StackView.view.push(Qt.resolvedUrl("HistoryDetailPage.qml"),
                {"entryName": name, "entryDate": date, "entryOffset": offset})
        }

        Label {
            anchors.centerIn: parent
            visible: historyList.count === 0
            text: qsTr("No saved conversations yet.")
            opacity: 0.6
        }
    }
}
