package de.tron.client_java.gui.view;

import java.io.IOException;
import java.util.Map.Entry;

import de.tron.client_java.App;
import de.tron.client_java.gui.model.LobbyViewModel;
import javafx.animation.Interpolator;
import javafx.animation.Transition;
import javafx.collections.ObservableMap;
import javafx.fxml.FXML;
import javafx.fxml.FXMLLoader;
import javafx.scene.control.Label;
import javafx.scene.layout.AnchorPane;
import javafx.scene.layout.HBox;
import javafx.scene.layout.VBox;
import javafx.scene.paint.Color;
import javafx.util.Duration;

public class LobbyView extends AnchorPane {
	
	@FXML private Label title;
	@FXML private VBox playerContainer;
	@FXML private HBox readyButtonBox;
	
	private LobbyViewModel viewModel;
	
	public LobbyView() {
		FXMLLoader fxmlLoader = new FXMLLoader(App.class.getResource("Lobby.fxml"));
        fxmlLoader.setRoot(this);
        fxmlLoader.setController(this);
        
        try {
            fxmlLoader.load();
        } catch (IOException exception) {
            throw new RuntimeException(exception);
        }
    }
	
	public Transition getTransition(boolean reverse) {
		readyButtonBox.setOpacity(0);
		Transition transition = new Transition() {
			{
				setCycleDuration(Duration.seconds(2));
			}
			@Override
			protected void interpolate(double frac) {
				if (reverse) {
					frac = Math.abs(frac - 1);
				}
				
				title.setOpacity(Math.min(1, frac * 2));
				AnchorPane.setLeftAnchor(title, frac * 100);
				
				playerContainer.setOpacity(Math.max(0, frac * 2 - 1));
				AnchorPane.setLeftAnchor(playerContainer, frac * 200 - 60);
				
				if (frac == 1) {
					readyButtonBox.setOpacity(1);
				}
			}
		};
		transition.setInterpolator(Interpolator.LINEAR);
		transition.setOnFinished(e -> setVisible(!reverse));
		return transition;
	}
	
	@FXML
	private void ready() {
		this.viewModel.readyToPlay();
		this.readyButtonBox.setDisable(true);
	}
	
	private void refreshPlayers(ObservableMap<String, Color> newMap) {
		this.playerContainer.getChildren().clear();
		for (Entry<String, Color> playerEntry : newMap.entrySet()) {
			LobbyPlayerView playerView = new LobbyPlayerView();
			playerView.setPlayerName(playerEntry.getKey());
			playerView.setPlayerColor(playerEntry.getValue());
			this.playerContainer.getChildren().add(playerView);
		}
		this.playerContainer.getChildren().add(this.readyButtonBox);
	}
	
	private void bindProperties() {
		this.viewModel.playersProperty().addListener((p,o,n) -> refreshPlayers(n));	
		this.viewModel.refresh();
	}

	public void setViewModel(LobbyViewModel lobbyViewModel) {
		this.viewModel = lobbyViewModel;
		bindProperties();
	}


}
